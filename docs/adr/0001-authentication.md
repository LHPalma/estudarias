# ADR-001 — Autenticação: conta separada do aluno, senha com pepper versionado e sessão com refresh token rotativo

- **Status:** aceita
- **Referência:** issue #35 (Autenticação) e suas sub-issues. Os requisitos estão no SRS do módulo (`docs/SRS/SRS - Módulo de Autenticação.md`, PR #50).

## Contexto

A API era aberta e não havia conta de acesso: o `Student` guarda só o perfil de estudo (nome, grupo experimental e progresso), sem e-mail nem senha. O app (Expo) precisa que o aluno crie a própria conta, entre, mantenha a sessão sem digitar a senha toda hora e saia. E o TCC compara dois grupos de alunos, o de controle e o de diagnóstico, então cada aluno precisa ser identificado de forma estável e cair num grupo no cadastro, sem poder escolher nem trocar.

Os padrões vieram da autenticação do `ticketerias-api` (ADR-006), que por sua vez tirou a senha do `financarias` (Argon2id com pepper) e a sessão do `icrhub` (JWT curto e refresh token de uso único). A decisão foi adaptar esse padrão, e não inventar outro: já foi pensado e testado em PostgreSQL real no `ticketerias-api`. O que muda aqui é o transporte (REST, e não GraphQL), o fluxo de entrada (cadastro livre, e não criação por administrador) e o grupo experimental.

## Decisão

**Conta separada do aluno**
- `User` guarda a identidade (e-mail, hash da senha, papel e `StudentId`); `Student` guarda o perfil de estudo e o progresso. O `StudentId` liga os dois, e um aluno tem uma conta só (índice único).
- A separação mantém o domínio do estudo livre de credenciais, e deixa o papel de professor ou líder de equipe entrar depois sem tocar no aluno: a coluna `student_id` aceita nulo para papéis sem aluno.

**Cadastro livre, só o papel `Student`**
- O aluno se cadastra sozinho (`POST /auth/register`) e já recebe uma sessão aberta. Não há criação por administrador, senha temporária, troca obrigatória nem usuário inicial.
- O cadastro valida o e-mail e a senha **antes** de qualquer consulta ou custo de hash, recusa e-mail já cadastrado antes de sortear o grupo, e grava o aluno, a conta e o refresh token numa gravação só: se a conta falha, o aluno também não entra.
- A checagem prévia de e-mail não fecha a janela entre duas requisições simultâneas; quem fecha é o índice único. O banco recusa a segunda, a `EfUnitOfWork` traduz a violação (SQLSTATE 23505) numa exceção da Application, e o cadastro a transforma no mesmo conflito (`identity.email.taken`).

**Grupo experimental sorteado atrás de uma feature flag**
- O grupo é decidido no cadastro por uma porta (`IExperimentalGroupAssigner`), para trocar a estratégia sem mexer no caso de uso. Fica gravado no aluno e nunca muda.
- `ExperimentalGroup:RandomAssignment` (padrão `true`) liga o sorteio 50/50 entre `Control` e `Diagnostic`. Desligada, todo aluno novo cai em `ExperimentalGroup:FallbackGroup` (padrão `Diagnostic`, o app completo), sem sortear: serve para o período em que o experimento ainda não começou ou já terminou. A flag só vale para cadastros novos.
- A flag é lida pelo `IOptionsMonitor`, então virar o valor vale na chamada seguinte quando a configuração vem de um arquivo que recarrega. Um grupo de reserva que não existe derruba a subida.

**Senha**
- Argon2id com 19 MiB, 2 passes, 1 faixa e hash de 32 bytes: o mínimo recomendado pela OWASP. O **pepper é o `secret` do próprio Argon2**, não colado à senha. O pepper não aparece no texto PHC, então um dump do banco sozinho não permite ataque offline. O sal é por senha e público; o pepper é global e secreto, e um não substitui o outro.
- `PasswordHash` carrega o hash e a **versão do pepper** que o derivou. A configuração é um mapa versão → pepper e uma versão corrente, e a verificação busca o pepper pela versão gravada. Uma versão fora do mapa **lança** (é erro de configuração, não "senha errada": responder falso mostraria "senha errada" em silêncio a todo aluno afetado).
- A configuração é validada na subida: mapa não vazio, versão corrente presente e todo pepper, inclusive o de versão antiga, com 32 caracteres ou mais.
- Uma conta inexistente é conferida contra um hash descartável, gastando o mesmo trabalho, então o tempo de resposta não diz se o e-mail existe.
- O login **refaz o hash** quando a versão do pepper do hash não é a corrente (o único momento em que a senha em texto puro está à mão). Sem isso, um pepper antigo nunca poderia sair do mapa. Uma senha que já não cumpre a política mantém o hash antigo e entra normalmente.
- A política de composição (8 a 128 caracteres, com maiúscula, minúscula, dígito e caractere especial) vive no tipo `Password`, o único que o hasher aceita: nenhum hash sai de uma senha que ninguém validou.

**Sessão**
- **Access token:** JWT HS256 de 15 minutos, com as claims `sub` (id da conta), `email` e `role`, e nada mais de dado da conta. O middleware confere emissor, audiência, assinatura e validade, com 30 segundos de tolerância de relógio, e aceita **só o algoritmo HS256**: um token assinado com outro algoritmo, mesmo com a chave certa, ou sem assinatura, é recusado. A resposta traz também o instante de expiração, para o app renovar sem decodificar o JWT.
- **Refresh token:** 32 bytes aleatórios (64 caracteres hexadecimais), **só o hash SHA-256 é guardado**, de **uso único** e rotativo: usar um token o revoga e emite o sucessor, na mesma **família** (um login, uma família). Cada uso abre uma janela nova de 30 dias. Um hash rápido basta porque o token tem 256 bits de entropia e não é uma senha escolhida por uma pessoa.
- **Reuso detectado:** um token já revogado que reaparece só pode ter vazado, então a família inteira é revogada. As sessões de outros dispositivos (outras famílias) não são afetadas.
- **Rotação atômica:** `revoked_at` é o token de concorrência. De duas renovações simultâneas do mesmo token, o `UPDATE` da segunda não acha a linha ainda não revogada e falha, e ela recebe o mesmo erro de token inválido. Isso só se prova num PostgreSQL de verdade (o provider InMemory ignora o token de concorrência).
- **Logout** revoga o refresh token daquele dispositivo e é idempotente: um token desconhecido, vencido ou já revogado não é erro. O access token já emitido vale até expirar, porque é sem estado.
- O login apaga os refresh tokens vencidos do usuário, junto com o resto da gravação, para a tabela não crescer sem fim.

**Autorização**
- Login é exigido em toda a API por padrão (`FallbackPolicy` que exige usuário autenticado): um endpoint novo já nasce protegido, e quem precisa ser aberto declara `[AllowAnonymous]` de propósito. São abertos o `/health`, os quatro endpoints de sessão e, em Development, o OpenAPI.
- Uma rota que não existe também responde 401 a quem não tem token (e 404 a quem tem), o que não revela quais rotas existem.
- O papel só vale para a autorização que vier a existir; por ora há só `Student`.

**Erros**
- As falhas de domínio viram `application/problem+json` (RFC 9457) com o código do erro em `code`, e é por ele que o app decide o que mostrar. Validação é 400, credencial ou refresh token recusados é 401 (com o desafio `Bearer`) e conflito é 409. Qualquer outra exceção é um 500 genérico, sem `code` e sem detalhe técnico.
- Credenciais inválidas têm uma resposta só para e-mail malformado, e-mail desconhecido, senha errada e conta desabilitada, para a resposta não revelar qual parte estava errada.

**Persistência**
- Tabelas e colunas em inglês, em snake_case e no singular: `student`, `app_user` e `refresh_token`. A tabela do usuário é `app_user` porque `user` é palavra reservada do PostgreSQL (`SELECT * FROM user` devolve o usuário do banco, não a tabela).
- Unicidade, chaves estrangeiras e os valores permitidos (grupo experimental e papel) ficam no banco, que é quem fecha as corridas entre requisições.

**Configuração e segredos**
- O pepper e a chave do JWT ficam **expostos de propósito** no `appsettings.Development.json`, que só é lido em Development, com aviso no próprio arquivo. Um segredo versionado não protege nada: eles existem para a API subir sem configuração extra. Fora de Development vêm de variáveis de ambiente (`PasswordHashing__Peppers__N`, `PasswordHashing__CurrentPepperVersion`, `Jwt__SigningKey`, `ConnectionStrings__Default`).
- A ausência do pepper, da chave do JWT ou da connection string, uma chave curta, uma vida de token que não é positiva e o grupo de reserva inexistente derrubam a subida (a connection string, na composição; as demais, por `ValidateOnStart`): melhor falhar no boot do que no primeiro login. A chave do JWT é o segredo mais sensível, porque com ela se forja o token de qualquer aluno; trocá-la derruba só os access tokens em circulação (no máximo 15 minutos), e os refresh tokens seguem valendo.

## Alternativas consideradas

- **ASP.NET Core Identity.** Não adotado. Traz o seu próprio modelo de usuário e de armazenamento, acoplado ao EF Core, o que fura a regra deste projeto de manter o Domain sem dependências. Os padrões que precisamos (Argon2id com pepper versionado, refresh token com família e detecção de reuso) teriam de ser escritos por cima de qualquer jeito.
- **PBKDF2 ou bcrypt no lugar do Argon2id.** Não adotado: o Argon2id (memory-hard) é a recomendação atual da OWASP, e a biblioteca aceita o pepper como `secret` nativo.
- **JWT longo, sem refresh token.** Não adotado: sem estado no servidor, um token vazado não teria como ser revogado até expirar. O par access curto mais refresh rotativo limita o estrago e permite o logout de verdade.
- **Só sessão opaca no servidor (sem JWT).** Não adotado: cada requisição passaria pelo banco. O JWT curto deixa a validação sem estado, e o estado fica só no refresh token.
- **Cookies.** Não adotado: o cliente é um app móvel, que guarda e manda tokens, e não um navegador.
- **Login social, OAuth2 e OpenID Connect.** Fora do escopo: não foram avaliados.
- **Sorteio balanceado (alternância ou por blocos) no lugar do sorteio simples.** Não adotado por ora. O sorteio simples 50/50 pode deixar os dois grupos com tamanhos diferentes numa amostra pequena. Se isso virar um problema para a análise do experimento, a estratégia se troca implementando a porta `IExperimentalGroupAssigner`, sem mexer no cadastro.

## Fora do escopo

- Papéis além de `Student`, equipes e permissões por equipe.
- Recuperação e troca de senha, e verificação de e-mail (exigem envio de e-mail).
- Bloqueio por tentativas erradas e limite de taxa no login.
- Revogar o access token antes de expirar, e "sair de todos os dispositivos".
- Login social.
- Um endpoint de perfil (`me`): a resposta de sessão traz o `studentId`, mas não o nome do aluno, que está no perfil de estudo.

## Consequências

- **Quebra de contrato:** toda a API exige login. O app ganha as telas de cadastro e de login, e o tratamento de `401` renovando a sessão.
- **O access token não é revogável:** quem perde o acesso (conta desabilitada, logout) ainda passa pelo middleware por até 15 minutos, porque revogá-lo antes exigiria estado no servidor. A conta desabilitada, porém, não renova a sessão nem entra de novo.
- **Sem limite de tentativas no login:** o custo do Argon2id encarece cada tentativa, mas não impede adivinhar uma senha pela rede, e também faz de cada tentativa (19 MiB, 2 passes) um custo de CPU para o servidor. O bloqueio por tentativas e o limite de taxa ficam como a melhoria mais relevante de segurança, a decidir quando houver uso real.
- **O cadastro revela se um e-mail já existe** (o 409), e **não há verificação de posse do e-mail**: qualquer pessoa pode cadastrar qualquer endereço. É o que um cadastro livre sem envio de e-mail permite. Na recuperação de senha futura isso precisa ser revisto.
- **Os refresh tokens vencidos de um usuário só são apagados quando ele volta a entrar:** uma conta abandonada mantém os dela. O volume é pequeno, mas não é zero.
- **A chave do JWT e o pepper são segredos de operação.** Perder o pepper deixa as senhas existentes sem como serem verificadas; a rotação segue o procedimento do README (acrescentar a versão nova sem tirar a antiga, e remover a antiga só quando nenhum hash a usar).
- **Os testes de concorrência e de unicidade rodam num PostgreSQL de verdade** (Testcontainers; onde não há Docker, `ESTUDARIAS_TEST_POSTGRES`), nunca com o provider InMemory.
- **Revisitar quando:** entrarem equipes e papéis de professor ou líder; houver envio de e-mail (recuperação de senha e verificação); houver tráfego real que peça limite de taxa; ou o tamanho da amostra pedir um sorteio balanceado.
