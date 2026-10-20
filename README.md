# Estudarias

Plataforma adaptativa de estudo de matemática (TCC de ADS, FATEC São Caetano do Sul): trilha em grafo de pré-requisitos, diagnóstico do tipo de erro por alternativa e flashcards com repetição espaçada (SM-2).

## Arquitetura

Clean Architecture em três projetos (.NET 10, C#):

| Projeto | Responsabilidade | Depende de |
|---|---|---|
| `Estudarias.Domain` | Entidades, regras de negócio e motor (nota, desbloqueio, SM-2) | nada |
| `Estudarias.Application` | Casos de uso, DTOs e abstrações (repositórios, relógio) | Domain |
| `Estudarias.Infrastructure` | EF Core + PostgreSQL, seed dos YAML, implementações das abstrações | Application |

O cliente mobile (Expo + TypeScript) consome a API REST. Toda a lógica fica no back-end: o app só exibe e envia respostas.

## Documentação

- [`docs/schema.dbml`](docs/schema.dbml): schema alvo da v1, com dados de exemplo (Frações).

## Rodando

```bash
dotnet build
```
