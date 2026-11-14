# Implementation Plan: Cadastro de usuário e acesso por e-mail

**Branch**: `docs/cadastro-usuario` | **Date**: 2026-11-12 | **Spec**: [spec.md](spec.md)

**Input**: `specs/004-user-registration/spec.md`, incluindo as três clarificações de 2026-11-12.
**Status**: plano para revisão; implementação não iniciada.

## Summary

Adicionar cadastro inicial do único titular, login por e-mail e conclusão obrigatória do cadastro legado, preservando `Owner.Id` e todos os vínculos financeiros. Reutilizar Identity, sessões SQL e CSRF. A comprovação legada gera uma autorização restrita por dez minutos, sem sessão financeira. Concluir a transição atualiza perfil e credencial e revoga sessões e autorizações em uma transação.

A estrutura permanece usuário → contas financeiras → movimentações, e usuário → obrigações. A baixa associa obrigação e movimentação. Toda operação financeira exige login válido e titularidade verificada no servidor.

## Technical Context

| Área | Decisão |
|---|---|
| Language/Version | C# 14, SDK .NET 10.0.403; Angular 22.2.5, TypeScript 6.0.3, Node 24.22.2. |
| Primary Dependencies | Identity/EF Core SQL Server 10.0.14 e RxJS 7.8.2. Reutilizar versões e lockfiles existentes, sem novos pacotes. |
| Storage | SQL Server; migração incremental após a 003, validada em SQL Server 2025 Express LocalDB e na integração. |
| Testing | xUnit v3 4.0.3/Microsoft Testing Platform, WebApplicationFactory, TimeProvider controlado; Angular/Vitest 4.0.8; navegador e API. |
| Target Platform | Aplicação web responsiva, HTTPS em localhost, um titular por instalação. |
| Project Type | Monólito modular DDD/MVC com Domain, Application, Infrastructure, Api e Angular. |
| Performance Goals | Spec sem novo SLA de latência. Login não carrega histórico financeiro. Preservar limitador existente de dez tentativas/minuto por IP e bloqueio após cinco falhas por cinco minutos. |
| Constraints | Senha contada por pontos de código Unicode; nascimento civil em America/Sao_Paulo; relógio injetável; autorização absoluta de dez minutos desde a comprovação das credenciais antigas (VerifiedAtUtc), sem reinício por espera ou emissão, inatividade financeira de trinta minutos. |
| Scale/Scope | Cadastro, login, saída, restauração e transição legada. Sem recuperação, edição posterior de perfil/senha, verificação de contatos ou múltiplos usuários. |

## Constitution Check

Gate de entrada: checklist de requisitos 16/16, clarificações respondidas e solicitação explícita para planejar. O status Draft da spec não é alterado por este comando. Não há tarefas preexistentes da 004.

| Princípio | Antes da pesquisa | Após o design |
|---|---|---|
| I · Especificação | FR-001–017 e SC-001–007 identificados. | Matriz rastreia requisitos e verificações; tarefas ficam para a próxima fase. |
| II · DDD/MVC/SQL | Stack existente compatível e fixada. | Domínio valida; aplicação coordena por portas; infraestrutura implementa Identity/SQL; controllers somente adaptam HTTP. |
| III · Integridade | Identidade estável obrigatória. | Migração/transição preservam FKs, valores, baixas e totais; mutações compostas atômicas, auditoria UTC. |
| IV · Segurança | HTTPS, CSRF e sessões existentes. | Autorização restrita não autentica; cadastro incompleto bloqueia sessões antigas; dados pessoais e segredos fora dos logs. |
| V · Integrações | Nenhuma integração externa. | Origens financeiras permanecem intactas. |
| VI · Testes | xUnit para regras e casos de uso. | SQL real para migração/concorrência/rollback, contratos HTTP e navegador definidos. |

Resultado antes da pesquisa e após a fase 1: PASS no desenho, sem exceções. Isso não significa testes do produto executados ou implementação aprovada.

## Project Structure

### Documentation (this feature)

```text
specs/004-user-registration/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── api.md
│   └── ui.md
└── checklists/requirements.md
```

`tasks.md` será gerado somente por `$speckit-tasks`, após revisão deste plano.

### Source Code (repository root)

| Local | Responsabilidade planejada |
|---|---|
| `src/backend/FinanceManager.Domain/Authentication/` | Valores de perfil, política de senha e `RegistrationAuthorization`, sem Identity/EF/HTTP. |
| `src/backend/FinanceManager.Application/Authentication/` | `OwnerRegistrationService`, evolução de `OwnerAccessService`, portas e resultados separados de sessão/transição. |
| `src/backend/FinanceManager.Infrastructure/Authentication/` | Evoluir `Owner`, `IdentityOwnerAdapter`, `OwnerTicketStore` e `SqlOwnerSessionStore`; adaptar validadores Identity. |
| `src/backend/FinanceManager.Infrastructure/Persistence/` | Registro/transição transacionais, autorizações, auditoria e migração. |
| `src/backend/FinanceManager.Api/Controllers/` e `Authentication/` | DTOs, cookies, filtros e HTTP; sem DbContext/repositórios nos controllers. |
| `src/backend/FinanceManager.Api/Commands/` | Retirar provisionamento manual do fluxo de produção e orientar cadastro web. |
| `src/frontend/src/app/auth/` e `app.routes.ts` | Login/cadastro/transição, restauração de sessão, tratamento de expiração e erros. |
| `tests/FinanceManager.UnitTests/Authentication/` e `tests/FinanceManager.IntegrationTests/Authentication/` | Regras, serviços, HTTP, Identity, SQL, migração, segurança e concorrência. |

**Structure Decision**: ampliar o módulo de autenticação existente, mantendo módulos financeiros e identidade única.

## Estratégia de design

1. Modelar regras puras e a migração conforme [data-model.md](data-model.md). Índice e check SQL garantem o único titular; data de conclusão distingue legado e cadastro definitivo.
2. Cadastro inicial grava perfil, hash e auditoria atomicamente, sem sessão. Conflitos nunca substituem o titular existente.
3. Login definitivo pesquisa somente e-mail normalizado. **Usar acesso antigo** no login e o caminho **Criar conta → Completar cadastro existente** compartilham a comprovação legada. Credenciais antigas válidas não criam `OwnerSession`.
4. Autorização opaca em cookie separado, hash no SQL, expiração fixa. Emissão e conclusão sincronizam a linha do titular; conclusão revalida estado/prazo e atualiza perfil/hash, revoga todas as sessões/autorizações e audita em um commit. Ver [research.md](research.md).
5. Rejeitar titular incompleto na reconstrução e admissão da sessão. Proteger todas as rotas financeiras, inclusive obrigações, pela API; Angular somente orienta navegação.
6. Adaptar contratos e fixtures antigos de autenticação sem enfraquecer regressões financeiras. Preparação de dados legados fica nas fixtures sobre a migração anterior; produção usa cadastro web.
7. Criar formulários acessíveis e responsivos, mensagens por campo em português e estado não secreto somente em memória. Não guardar senhas ou perfil em localStorage/sessionStorage.

## Matriz de verificação

| Requisitos / resultados | Verificação planejada |
|---|---|
| FR-001–002 | Angular/navegador: login inicial, Criar conta/voltar, sessão válida, cadastro pendente e indisponibilidade. |
| FR-003–004; SC-002 | xUnit domínio/aplicação, HTTP e formulário: ausentes, espaços, acentos/apóstrofos, apelido ausente, datas inválida/futura/hoje e virada de dia de São Paulo. |
| FR-005–006; SC-002–003 | Caixa/trim do e-mail identificam mesmo titular; cada formato inválido; telefone com 9/10/15/16 dígitos, formatação e caracteres proibidos. |
| FR-007; SC-002 | xUnit/Angular: 11/12/128/129 pontos de código, categorias ausentes, Unicode suplementar, espaços e senha exata; Identity não impõe política divergente. |
| FR-008, FR-014; SC-004 | SQL/HTTP: cadastros simultâneos com e-mails iguais/distintos, reenvio após resposta perdida e falha entre gravações; nenhuma identidade parcial/adicional. |
| FR-009–011; SC-001, SC-003 | HTTP/navegador: login exclusivo por e-mail, sem autenticação automática, destino com zero contas/ativas/somente arquivadas e reinício. Cinco participantes usando dados sintéticos: pelo menos quatro concluem em cinco minutos sem assistência. |
| FR-012–013; SC-006 | xUnit/HTTP: todas as rotas financeiras com sessão ausente/incompleta/revogada/expirada, cookie restrito e IDs alheios; CSRF nas mutações; 30 minutos, limitação, bloqueio e ausência de dados pessoais nos logs. |
| FR-015–016; SC-004–005 | Migração e transição com histórico sintético: comparar IDs, FKs, contagens, valores e vínculos. Duas conclusões, prova/sessão concorrente, rollback e replay dos cookies antigos após commit. |
| FR-017; SC-007 | Relógio controlado: antes/exatamente/depois de dez minutos desde VerifiedAtUtc, atraso entre comprovação e emissão sem extensão do prazo, emissão já expirada sem cookie utilizável, atividade sem renovação e nova prova preservando dados não secretos. |

Executar [quickstart.md](quickstart.md) após implementação; registrar evidências nas futuras tarefas. Todos os FRs têm cobertura unitária de suas regras/casos de uso e cobertura de apresentação/contrato quando aplicável.

## Complexity Tracking

Sem exceções constitucionais. A autorização restrita atende FR-015–017 e não substitui a sessão financeira.
