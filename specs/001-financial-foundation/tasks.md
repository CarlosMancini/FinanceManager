---
description: "Tarefas de implementação da Fundação financeira"
---

# Tarefas · 001 · Fundação financeira

**Entrada** · [Especificação](spec.md), [plano](plan.md), [pesquisa](research.md), [modelo de dados](data-model.md), [contrato HTTP](contracts/api.md) e [guia de validação](quickstart.md).

**Testes** · Cada regra de negócio exige testes unitários xUnit para resultados, rejeições e limites aplicáveis. Os testes de contrato e integração verificam as garantias de HTTP e SQL Server.

**Formato** · `- [ ] Tnnn [P] [USn] Descrição com caminho`. Os caminhos são relativos à raiz. `[P]` indica tarefas que podem rodar juntas depois que suas dependências forem concluídas. A etiqueta de história aparece somente nas fases de US1 e US2.

## Fase 1 · Configuração compartilhada

- [X] T001 Fixar SDK .NET 10.0.403 e runner Microsoft Testing Platform em `global.json`, versões ASP.NET Core, Identity, EF Core, xUnit e FakeTimeProvider conforme `specs/001-financial-foundation/research.md` em `Directory.Packages.props`, e dotnet-ef 10.0.14 em `.config/dotnet-tools.json`. Habilitar lockfiles NuGet em `Directory.Build.props`.
- [X] T002 [P] Criar `FinanceManager.slnx` e os projetos `src/backend/FinanceManager.Api/FinanceManager.Api.csproj`, `src/backend/FinanceManager.Application/FinanceManager.Application.csproj`, `src/backend/FinanceManager.Domain/FinanceManager.Domain.csproj` e `src/backend/FinanceManager.Infrastructure/FinanceManager.Infrastructure.csproj` em net10.0. Manter o domínio independente e referenciar os adaptadores pela raiz de composição da API.
- [X] T003 [P] Inicializar a SPA, fixar Angular e CLI 22.2.5 e TypeScript 6.0.3 em `src/frontend/package.json`, Node 24.22.2 em `src/frontend/.node-version` e salvar `src/frontend/package-lock.json`. Configurar `src/frontend/angular.json` para produzir `src/frontend/dist/finance-manager/browser`.
- [X] T004 Adicionar os projetos xUnit `tests/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj` e `tests/FinanceManager.IntegrationTests/FinanceManager.IntegrationTests.csproj` a `FinanceManager.slnx`. Gerar os lockfiles e verificar restore bloqueado, build e descoberta de testes. Depende de T002.

## Fase 2 · Fundação que bloqueia as histórias

- [X] T005 [P] Preparar EF Core com o provedor SQL Server e a conexão `ConnectionStrings:FinanceManager` em `src/backend/FinanceManager.Infrastructure/Persistence/FinanceManagerDbContext.cs`, `src/backend/FinanceManager.Infrastructure/Persistence/FinanceManagerDbContextFactory.cs` e `src/backend/FinanceManager.Infrastructure/DependencyInjection.cs`. Manter configuração e segredos locais fora do repositório.
- [X] T006 [P] Configurar MVC, HTTPS somente em loopback, hospedagem da SPA por `Angular:BrowserRoot`, relógio `TimeProvider` e respostas de erro em português em `src/backend/FinanceManager.Api/Program.cs` e `src/backend/FinanceManager.Api/Http/ApiProblems.cs`. A navegação da SPA usa fallback separado de `/api`. Impedir registro de credenciais, cookies, tokens e dados financeiros.
- [X] T007 Conectar a infraestrutura ao host em `src/backend/FinanceManager.Api/Program.cs` e preparar `WebApplicationFactory`, relógio controlável e banco SQL Server isolado por cenário ou conjunto de cenários em `tests/FinanceManager.IntegrationTests/Fixtures/FinanceManagerApiFactory.cs` e `tests/FinanceManager.IntegrationTests/Fixtures/SqlServerTestDatabase.cs`. Usar `ConnectionStrings:FinanceManagerTests`, dados sintéticos e aplicação de migrações. Depende de T005 e T006.

**Ponto de verificação** · Solução e SPA compilam. A fixture consegue iniciar o host e usar uma base SQL Server de teste separada.

## Fase 3 · US1 · Acessar a aplicação como titular (P1, MVP)

**Objetivo** · Provisionar um titular local, entrar, consultar a sessão e sair com revogação no servidor.

**Teste independente** · Provisionar e autenticar o titular. Confirmar que `GET /api/auth/session` responde 200 durante a sessão e 401 antes do login, após logout e ao completar 30 minutos sem atividade. Reapresentar um cookie copiado após sair e confirmar 401.

### Testes da US1

- [X] T008 [P] [US1] Escrever testes unitários xUnit de sessão ausente, revogada e expirada, 29 min 59 s e limite exato de 30 min, relógio em UTC, atividade admitida que reinicia o prazo, rejeição que não renova e revogação em `tests/FinanceManager.UnitTests/Authentication/OwnerSessionTests.cs`. FND-01.
- [X] T009 [P] [US1] Escrever testes unitários xUnit de provisionamento único, credenciais aceitas e rejeitadas, bloqueio temporário, emissão de sessão só após login válido e revogação do identificador autenticado em `tests/FinanceManager.UnitTests/Authentication/OwnerAccessServiceTests.cs`. FND-01.
- [X] T010 [P] [US1] Escrever testes de contrato para `GET /api/auth/csrf`, `POST /api/auth/login`, `GET /api/auth/session` e `POST /api/auth/logout` em `tests/FinanceManager.IntegrationTests/Authentication/AuthContractTests.cs`. Conferir cookies, proteção CSRF, cache, status e códigos estáveis do contrato, inclusive 401, 403 e 429. FND-01.
- [X] T011 [P] [US1] Escrever testes de integração SQL Server de provisionamento simultâneo, expiração no limite, renovação somente após admissão, logout e replay, atividade concorrente sem retrocesso e tentativa de renovar sessão vencida ou revogada em `tests/FinanceManager.IntegrationTests/Authentication/OwnerSessionPersistenceTests.cs`. FND-01.

### Implementação da US1

- [X] T012 [US1] Modelar `OwnerSession` em `src/backend/FinanceManager.Domain/Authentication/OwnerSession.cs`. Aplicar “Identificador aleatório de alta entropia. O cookie carrega uma referência protegida.”, “FK para o titular autenticado.”, “Instantes UTC. Ambos começam no login.” e “Opcional. O logout registra a revogação antes de limpar o cookie.”. A sessão só fica ativa antes de `LastActivityAtUtc + 30 min`, se ainda não revogada. FND-01.
- [X] T013 [US1] Implementar provisionamento, login, consulta, admissão e logout por serviços e portas em `src/backend/FinanceManager.Application/Authentication/OwnerAccessService.cs`, `src/backend/FinanceManager.Application/Authentication/IOwnerIdentity.cs` e `src/backend/FinanceManager.Application/Authentication/IOwnerSessionStore.cs`. Contar atividade após autenticação, autorização da rota e antiforgery aplicável, antes do caso de uso. Excluir estáticos, emissão de token CSRF, requisições barradas e polling. Depende de T012. FND-01.
- [X] T014 [US1] Integrar Identity, hash, bloqueio de tentativas e provisionamento local com prompt protegido em `src/backend/FinanceManager.Infrastructure/Authentication/Owner.cs`, `src/backend/FinanceManager.Infrastructure/Authentication/IdentityOwnerAdapter.cs` e `src/backend/FinanceManager.Api/Commands/ProvisionOwnerCommand.cs`. Aplicar “`Id` (`Guid`) é a identidade estável do titular usada nas contas e nas sessões.” e “Um titular tem zero ou mais sessões e contas.”. Garantir o titular único de forma transacional, inclusive sob concorrência. Depende de T013. FND-01.
- [X] T015 [US1] Persistir a sessão em `src/backend/FinanceManager.Infrastructure/Authentication/SqlOwnerSessionStore.cs` e `src/backend/FinanceManager.Infrastructure/Authentication/OwnerTicketStore.cs`. Verificar validade e avançar a atividade atomicamente, sem retrocesso. Derivar expiração do ticket da última atividade, revogar antes de limpar o cookie e preservar a atividade durante renovação de metadados. Versionar o esquema Identity e sessão na migração `InitialIdentityAndSessions` em `src/backend/FinanceManager.Infrastructure/Persistence/Migrations/`. Depende de T014. FND-01.
- [X] T016 [US1] Expor os quatro endpoints de autenticação em `src/backend/FinanceManager.Api/Controllers/AuthController.cs`. Integrar admissão, cookies Secure/HttpOnly/SameSite=Lax, antiforgery inclusive no login/logout, bloqueio por origem de conexão, chaves protegidas e respostas 401/403/429 em `src/backend/FinanceManager.Api/Authentication/SessionAdmissionFilter.cs` e `src/backend/FinanceManager.Api/Program.cs`. Controllers delegam aos serviços. Depende de T015. FND-01.
- [X] T017 [US1] Implementar login, estado autenticado, logout e aviso de sessão expirada em `src/frontend/src/app/auth/login.component.ts`, `src/frontend/src/app/auth/auth.service.ts` e `src/frontend/src/app/app.routes.ts`. Usar URLs relativas, atualizar token CSRF após mudança de identidade e evitar polling. Executar o teste independente da US1 e as suítes T008–T011. Depende de T016. FND-01.

**Ponto de verificação** · US1 pode ser validada no navegador e pela API de sessão, antes da gestão de contas.

## Fase 4 · US2 · Gerenciar contas financeiras (P2)

**Objetivo** · Cadastrar, consultar, editar, arquivar e reativar contas do titular, com proteção contra nomes duplicados e versões antigas.

**Teste independente** · Em uma sessão válida, cadastrar, editar, arquivar e reativar uma conta pela interface. Verificar persistência após reinício, rejeição de nome reservado por conta arquivada e rejeição do salvamento desatualizado em duas abas.

### Testes da US2

- [X] T018 [P] [US2] Escrever testes unitários xUnit de nome obrigatório, limites 120/121 após trim, normalização de caixa, NFC, espaços e acentos, tipos permitidos e inválidos, BRL sem saldo inicial e transições que preservam a identidade e a disponibilidade correta em `tests/FinanceManager.UnitTests/Accounts/FinancialAccountTests.cs`. FND-02, FND-03, FND-04.
- [X] T019 [P] [US2] Escrever testes unitários xUnit dos casos de uso de conta em `tests/FinanceManager.UnitTests/Accounts/FinancialAccountServiceTests.cs`. Cobrir consultas por titular, filtros, duplicatas em cadastro e renomeação com conta ativa ou arquivada, nome da própria conta e de outro titular, revisões atuais e antigas, rejeição de recurso de outro titular e falha de persistência sem sucesso. FND-01, FND-02, FND-03, FND-04.
- [X] T020 [P] [US2] Escrever testes de contrato dos cinco endpoints de contas em `tests/FinanceManager.IntegrationTests/Accounts/AccountsContractTests.cs`. Conferir lista vazia, filtros, 201 e `Location`, ETag, sessão obrigatória, CSRF, 404 fora do titular e erros em português para entrada inválida, nome em conflito e `If-Match` ausente, inválido ou desatualizado. FND-01, FND-02, FND-03, FND-04.
- [X] T021 [P] [US2] Escrever testes com SQL Server para migração, reinício do host com o mesmo banco, inserções e renomeações concorrentes, colisão com conta arquivada, edição concorrente com arquivamento e atomicidade entre alteração e auditoria em `tests/FinanceManager.IntegrationTests/Accounts/FinancialAccountPersistenceTests.cs`. Usar titular sintético adicional em cenário isolado para validar escopo do índice e das consultas. FND-01, FND-02, FND-03, FND-04.

### Implementação da US2

- [X] T022 [US2] Modelar `FinancialAccount` em `src/backend/FinanceManager.Domain/Accounts/FinancialAccount.cs`. Aplicar “`Guid` estável, gerado no servidor.”, “FK obrigatória. Titular imutável.”, “Nome de exibição obrigatório, aparado nas extremidades, de 1 a 120 unidades UTF-16. Preserva caixa e grafia.” e “Chave obrigatória derivada por trim, NFC, caixa alta invariante e NFC. Acentos e espaços internos continuam significativos.”. Aplicar também “`checking`, `savings` ou `wallet`.”, “`BRL`, definida no cadastro e imutável.” e “`active` no cadastro, com transições `active → archived → active`.”. Preservar identidade e histórico e excluir contas arquivadas da seleção de novas movimentações. FND-02, FND-03, FND-04.
- [X] T023 [US2] Implementar cadastro, listagem e filtros, detalhe, edição e mudança de estado em `src/backend/FinanceManager.Application/Accounts/FinancialAccountService.cs` e `src/backend/FinanceManager.Application/Accounts/IFinancialAccountRepository.cs`. Obter `OwnerId` da sessão e buscar por `OwnerId` e `Id`. Validar unicidade em cadastro e renomeação, preservar dados em versão antiga e coordenar a gravação com auditoria. Depende de T022. FND-01, FND-02, FND-03, FND-04.
- [X] T024 [US2] Implementar consultas e gravação em `src/backend/FinanceManager.Infrastructure/Persistence/SqlFinancialAccountRepository.cs` e `src/backend/FinanceManager.Infrastructure/Persistence/FinancialAccountConfiguration.cs`. Aplicar “`rowversion` do SQL Server. Identificador opaco de revisão, exposto via ETag.”. Criar o índice único não filtrado `UX_FinancialAccounts_OwnerId_NormalizedName` em `(OwnerId, NormalizedName)` com chave `nvarchar(450)` e `Latin1_General_100_BIN2`. Traduzir conflito do índice e da revisão, gravar auditoria mínima UTC na mesma transação e versionar a migração `AddFinancialAccounts` em `src/backend/FinanceManager.Infrastructure/Persistence/Migrations/`. Depende de T023 e da migração T015. FND-02, FND-03, FND-04.
- [X] T025 [US2] Expor lista, cadastro, detalhe, edição e estado em `src/backend/FinanceManager.Api/Controllers/AccountsController.cs` e `src/backend/FinanceManager.Api/Http/AccountVersionHeaders.cs`. Delegar casos de uso, devolver `Cache-Control: no-store`, usar ETag forte e exigir um único `If-Match` forte, com 400, 409, 412 e 428 conforme contrato. Aplicar autorização e CSRF da US1. Depende de T024 e T016. FND-01, FND-02, FND-03, FND-04.
- [X] T026 [US2] Implementar tela responsiva e cliente de contas em `src/frontend/src/app/accounts/accounts.component.ts`, `src/frontend/src/app/accounts/accounts-api.service.ts` e `src/frontend/src/app/app.routes.ts`. Exibir carregamento, lista vazia e erros de validação, sessão e gravação. Ler a conta e a ETag antes de editar ou mudar estado. Em 412, pedir recarga antes de permitir nova tentativa, sem reenvio automático. Executar o teste independente da US2 e as suítes T018–T021. Depende de T025 e T017. FND-02, FND-03, FND-04.

**Ponto de verificação** · US2 pode ser verificada em uma sessão provisionada, com dados próprios e sem depender do roteiro de teste da US1.

## Fase 5 · Validação e revisão transversal

- [X] T027 Executar a restauração bloqueada de `FinanceManager.slnx`, as suítes `tests/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj` e `tests/FinanceManager.IntegrationTests/FinanceManager.IntegrationTests.csproj` e aplicar as migrações de `src/backend/FinanceManager.Infrastructure/Persistence/Migrations/` em uma base SQL Server vazia. Confirmar os resultados da matriz em `specs/001-financial-foundation/plan.md`.
- [X] T028 Percorrer o roteiro em `specs/001-financial-foundation/quickstart.md` com dados sintéticos, verificar navegador, API HTTPS em loopback e persistência após reinício. Corrigir instruções do guia se divergirem dos comandos efetivamente executados. Depende de T027.
- [X] T029 Revisar a entrega contra `.specify/memory/constitution.md` e `specs/001-financial-foundation/contracts/api.md`. Identificar os testes unitários que validam cada regra criada, verificar delegação dos controllers, mensagens em português e ausência de dados sensíveis nos logs. Depende de T028.

## Dependências e ordem de execução

```text
Fase 1 · Configuração
  → Fase 2 · Fundação
    → US1 · Acesso (FND-01, MVP)
      → US2 · Contas (FND-02–04 e isolamento FND-01)
        → Fase 5 · Validação e revisão
```

- **Configuração.** T002 e T003 podem avançar juntas após T001. T004 depende de T002. Finalizar toda a fase antes de iniciar a fundação.
- **Fundação.** T005 e T006 podem avançar juntas. T007 usa ambas.
- **Histórias.** Redigir os testes da história antes da implementação. Quando for preciso expor assinaturas para compilar, usar o mínimo necessário e observar a falha esperada antes de adicionar comportamento. Concluir US1 antes de integrar US2, pois contas usam o titular autenticado, a proteção CSRF e o esquema de identidade.
- **Validação.** T027–T029 dependem das duas histórias. Cobertura unitária por regra e verificação em SQL Server fazem parte do critério de conclusão.

## Exemplos de execução em paralelo

| História | Tarefas | Condição |
|---|---|---|
| US1 | T008 e T009 em arquivos unitários separados, T010 e T011 em arquivos separados de contrato e persistência | Fundação concluída. Cada cenário integrado usa dados isolados. |
| US2 | T018 e T019 em arquivos unitários separados, T020 e T021 em arquivos separados de contrato e persistência | US1 concluída. Cada cenário integrado usa dados isolados. |

## Estratégia de implementação

### MVP

Entregar a fundação e a US1. Validar provisionamento, login, expiração, logout e recusa de sessão ausente ou revogada pela API de autenticação e pela tela.

### Entrega incremental

Acrescentar a US2 com contas persistidas, arquivamento reversível, unicidade por titular e controle de edição concorrente. Finalizar com a matriz de testes e a validação no navegador e no SQL Server.
