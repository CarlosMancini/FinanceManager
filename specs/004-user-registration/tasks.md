# Tasks: Cadastro de usuário e acesso por e-mail

**Input**: design em `specs/004-user-registration/` — [spec](spec.md), [plano](plan.md), [pesquisa](research.md), [modelo](data-model.md), contratos [HTTP](contracts/api.md) e [UI](contracts/ui.md), [quickstart](quickstart.md).
**Branch**: `docs/cadastro-usuario` · **Data**: 2026-11-12.
**Prerequisites**: solicitação explícita de geração de tarefas após o planejamento; checklist de requisitos 16/16. Implementação depende da revisão desta lista e de comando próprio.
**Tests**: obrigatórios pela constituição VI e pela matriz do plano. Cada regra/caso de uso recebe testes xUnit, incluindo rejeições/limites; SQL Server real verifica persistência, migração e concorrência. Testes Angular e validação pela API/navegador complementam essa cobertura.
**Organization**: setup → fundação compartilhada → US1 → US2 → US3 → validação transversal. Todas as histórias são P1; ordem segue a spec e o reuso de código.

## Format: `[ID] [P?] [Story] Description`

`[P]` permite trabalho em arquivos distintos somente depois dos pré-requisitos descritos. `[US1]`, `[US2]` e `[US3]` identificam a história. Caminhos são relativos à raiz. Nomes de novos arquivos são decisões de execução dentro da arquitetura aprovada; arquivos existentes devem ser evoluídos sem duplicar regras.

Tarefas de teste aparecem antes da implementação: escrever os cenários e confirmar falha pelo comportamento ainda ausente; depois da implementação, executar novamente. Não marcar tarefa de teste como concluída enquanto seus resultados esperados não passarem. Os checkpoints exigem implementação e validação; este documento nasce integralmente desmarcado.

## Phase 1: Setup — preparação

**Objetivo**: preparar ambiente e fixtures sem atualizar stack ou alterar finanças.

- [X] T001 Conferir versões/lockfiles de `global.json`, `Directory.Packages.props`, `.config/dotnet-tools.json` e `src/frontend/package.json`, restaurar conforme `specs/004-user-registration/quickstart.md` e registrar baseline em `specs/004-user-registration/validation.md`, sem upgrades ou novas dependências.
- [X] T002 Preparar fixtures sintéticas compartilhadas em `tests/FinanceManager.IntegrationTests/Authentication/RegistrationScenario.cs` e `tests/FinanceManager.IntegrationTests/Authentication/LegacyRegistrationScenario.cs`: banco vazio, titular concluído para testes independentes de login, e banco na migração `20261111010901_AddBillsAndSettlements` com titular/hash/sessão e histórico financeiro, sem invocar bootstrap de produção depois da migração 004.

## Phase 2: Foundational — pré-requisitos bloqueantes

**Objetivo**: regras comuns às US1/US3, esquema de cadastro/transição e barreira de sessão segura. Não iniciar implementação das histórias antes deste checkpoint. Fixtures podem preparar um titular concluído diretamente na infraestrutura de teste, sem depender do endpoint de cadastro.

- [X] T003 [P] Criar e validar casos de perfil em `tests/FinanceManager.UnitTests/Authentication/RegistrationProfileTests.cs`: obrigatórios ausentes/espaços, grafia/trim, apelido vazio, datas inexistente/futura/hoje/virada de São Paulo, e-mail válido/inválido/caixa/trim, telefone com 9/10/15/16 dígitos, formatação e caracteres proibidos; incluir e-mail maior que 256 caracteres válido segundo FR-005. (FR-003–006)
- [X] T004 [P] Criar e validar política em `tests/FinanceManager.UnitTests/Authentication/RegistrationPasswordPolicyTests.cs`: 11/12/128/129 pontos de código, categorias ausentes, dígitos ASCII versus Unicode, símbolos/espaços, caracteres suplementares e preservação exata. (FR-007)
- [X] T005 Implementar perfil/normalização em `src/backend/FinanceManager.Domain/Authentication/RegistrationProfile.cs`: FullName “nvarchar(max), obrigatório quando concluído; trim externo, preservar grafia.”; Nickname “nvarchar(max) opcional; trim externo, vazio vira null.”; BirthDate “date/DateOnly, obrigatório quando concluído; existente, não futuro em America/Sao_Paulo via relógio injetável, sem idade mínima.”; Email/NormalizedEmail “nvarchar(max), obrigatórios quando concluído; um @ com partes não vazias, sem espaços internos; trim e caixa invariável. Comparação ordinal/collation binária coerente.”; PhoneNumber “Campo Identity, 10–15 dígitos mais `+` inicial opcional; remover espaços/parênteses/hífens, rejeitar letras/outros símbolos.” Manter tipos SQL somente no mapeamento; domínio recebe hoje civil calculado pelo relógio. (T003; FR-003–006)
- [X] T006 Implementar `src/backend/FinanceManager.Domain/Authentication/RegistrationPasswordPolicy.cs` conforme FR-007: “12–128 pontos de código”, maiúscula/minúscula Unicode, dígito ASCII e símbolo que exclui letras/dígitos/espaços; não aparar/truncar/mudar caixa, com avaliação separada dos critérios. (T004; FR-007)
- [X] T007 [P] Criar e validar `tests/FinanceManager.UnitTests/Authentication/RegistrationAuthorizationTests.cs` para validade antes/exatamente/depois de dez minutos, revogação e atividade sem renovação; implementar `src/backend/FinanceManager.Domain/Authentication/RegistrationAuthorization.cs` com Id “Guid gerado pelo servidor, chave primária.”, OwnerId “FK obrigatória ao titular comprovado, exclusão restrita; não é autoridade recebida do cliente.”, TokenHash “binary(32), SHA-256 de token aleatório de 32 bytes, índice único.”, VerifiedAtUtc/ExpiresAtUtc “datetimeoffset, prazo de exatamente dez minutos desde a comprovação das credenciais antigas.” e RevokedAtUtc “datetimeoffset opcional, preenchido para todas as autorizações ao concluir.” Domínio controla regras temporais sem gerar cookies/SQL. (FR-015–017)
- [X] T008 Evoluir `src/backend/FinanceManager.Infrastructure/Authentication/Owner.cs` e o mapeamento em `src/backend/FinanceManager.Infrastructure/Persistence/FinanceManagerDbContext.cs`: Id “Guid existente e imutável, preservado nas FKs financeiras.”; ProvisioningSlot “tinyint obrigatório, check `= 1`, índice único `UX_Owners_ProvisioningSlot`.”; RegistrationCompletedAtUtc “datetimeoffset opcional. Null = legado incompleto; preenchido = definitivo.”; UserName/NormalizedUserName “Campos internos Identity; novos usam ID técnico, legados mantêm nome para prova somente enquanto incompletos.”; credencial/bloqueio “Campos Identity existentes de hash/stamps/lockout. Nunca persistir senha original.” Mapear campos de T005 como nullable para legado e obrigatórios/não vazios no estado concluído; remover EmailIndex antes de ampliar NormalizedEmail, preservando índice de nome interno e comparação binária. (T005–006; FR-003–009/016)
- [X] T009 Mapear autorização de T007 e criar `src/backend/FinanceManager.Infrastructure/Persistence/RegistrationAudit.cs` em `src/backend/FinanceManager.Infrastructure/Persistence/FinanceManagerDbContext.cs`: autorização com FK restrita, índice único de hash/índice por titular/checks de dez minutos desde VerifiedAtUtc e revogação não anterior à comprovação; auditoria “Somente acréscimo: `Id`, `OwnerId` (FK restrita), `Action` (`Registered` ou `RegistrationCompleted`) e `OccurredAtUtc`.”; sem perfil/segredos e uma vez por sucesso. (T007–008; FR-008/013/016–017)
- [X] T010 Gerar migração `AddOwnerRegistration` e snapshot em `src/backend/FinanceManager.Infrastructure/Persistence/Migrations/`, preservando todos os IDs/FKs/hash legado; zero titulares permite cadastro, um fica incompleto com slot 1, mais de um aborta sem excluir/reagrupar dados. Criar/validar `tests/FinanceManager.IntegrationTests/Authentication/RegistrationMigrationTests.cs` para base nova/003, checks, índices, ordem de remoção de EmailIndex e preservação do histórico; não editar migrações anteriores. (T008–009; FR-008/016)
- [X] T011 Adicionar adaptadores `src/backend/FinanceManager.Infrastructure/Authentication/RegistrationUserValidator.cs` e `RegistrationPasswordValidator.cs` e registrá-los em `src/backend/FinanceManager.Infrastructure/DependencyInjection.cs`, reutilizando T005–006 sem regras Identity adicionais; criar/validar `tests/FinanceManager.IntegrationTests/Authentication/RegistrationIdentityValidationTests.cs` para Unicode, limites, e-mail longo e senha legada apenas verificada, sem exigir nova política na prova. (FR-003–007/015)
- [X] T012 Evoluir `src/backend/FinanceManager.Api/Http/ApiProblems.cs` e model binding em `src/backend/FinanceManager.Api/Program.cs` para `errors` por campo em português, sem ecoar valores; criar/validar `tests/FinanceManager.IntegrationTests/Http/ApiValidationContractTests.cs` para erros genéricos de binding/validação, usando controller exclusivo do host de teste no mesmo arquivo para campos ausentes/data malformada/JSON estrito, sem depender de DTOs ou endpoints de cadastro da US1. Preservar filtro CSRF em `src/backend/FinanceManager.Api/Authentication/SessionAdmissionFilter.cs`, inclusive mutações AllowAnonymous. (FR-003/013–014)
- [X] T013 Definir portas/resultados de cadastro, disponibilidade e transição em `src/backend/FinanceManager.Application/Authentication/IOwnerRegistrationStore.cs` e `RegistrationResults.cs`, com estados empty/legacyPending/completed, sem token bruto em DTO público ou OwnerId como autoridade; manter transações e Identity atrás das portas e registrar serviços conforme implementados em `src/backend/FinanceManager.Infrastructure/DependencyInjection.cs`. (FR-008/014–017)
- [X] T014 Bloquear titular incompleto em `src/backend/FinanceManager.Infrastructure/Authentication/OwnerTicketStore.cs` e `SqlOwnerSessionStore.cs`, incluindo criação/admissão/reconstrução e cookies anteriores à atualização; criar/validar `tests/FinanceManager.IntegrationTests/Authentication/IncompleteOwnerAccessTests.cs` para contas/movimentações/obrigações, mantendo proteção por titularidade e trinta minutos de inatividade. Usar fixture concluída para casos de acesso válido. (FR-012/016; SC-006)

**Checkpoint**: regras e esquema validados no SQL Server; nenhum cadastro incompleto ou cookie de transição acessa finanças. Não publicar incremento que deixe a instalação legada sem o fluxo US3 concluído.

## Phase 3: User Story 1 — Criar a conta do titular (P1)

**Goal**: cadastro pela interface em instalação vazia, uma identidade persistida atomicamente e retorno ao login sem autenticação automática.
**Independent Test**: base sem titular → login/Criar conta → preencher dados válidos sem apelido → confirmar retorno ao login e ausência de sessão; reiniciar e conferir persistência. Validar campos, concorrência, reenvio e rollback sem depender do login definitivo da US2.

### Tests for User Story 1

- [X] T015 [P] [US1] Criar e validar `tests/FinanceManager.IntegrationTests/Authentication/RegistrationContractTests.cs` para GET registration-state e POST register: estados, DTO, 201/mensagem, 400/erros por campo (inclusive cada campo ausente, data malformada e JSON com campos desconhecidos no DTO real de cadastro), 403 sem CSRF, 409 para titular concluído/incompleto, sem PII/cookie financeiro. (FR-001–008/010/013–014)
- [X] T016 [P] [US1] Criar e validar `tests/FinanceManager.IntegrationTests/Authentication/RegistrationPersistenceTests.cs` com dois cadastros sincronizados (e-mails iguais/distintos), reenvio após resposta perdida, falha entre gravações e reinício: um slot/titular/hash/perfil/auditoria de sucesso, sem parcial ou sobrescrita. (FR-008/014; SC-004)
- [X] T017 [P] [US1] Criar e validar `tests/FinanceManager.UnitTests/Authentication/OwnerRegistrationServiceTests.cs` para validação, estados disponíveis, coordenação atômica por porta, conflito e falha sem sucesso/sessão; testar rejeições de todos os campos via caso de uso. (FR-003–008/010/014)

### Implementation for User Story 1

- [X] T018 [US1] Implementar disponibilidade e registro inicial em `src/backend/FinanceManager.Application/Authentication/OwnerRegistrationService.cs`, usando T005–006/T013, resultados de conflito e falha explícitos e nenhuma criação de sessão. (T017; FR-003–008/010/014)
- [X] T019 [US1] Implementar registro inicial em `src/backend/FinanceManager.Infrastructure/Persistence/SqlOwnerRegistrationStore.cs`: slot 1, UserName técnico, perfil/hash Identity e evento Registered no mesmo DbContext/transação; restrição SQL decide disputa, rollback antes de reler estado e mapear somente violações conhecidas. (T016/T018; FR-008/013–014)
- [X] T020 [US1] Expor GET `/api/auth/registration-state` e POST `/api/auth/register` em `src/backend/FinanceManager.Api/Controllers/AuthController.cs`, DTOs em `src/backend/FinanceManager.Api/Controllers/RegistrationRequests.cs`, conforme contrato, delegando à aplicação e sem login automático. (T015/T018–019; FR-002–008/010/014)
- [X] T021 [P] [US1] Criar e validar `src/frontend/src/app/auth/registration.component.spec.ts` e `registration-validation.spec.ts` para obrigatórios, telefone/datas, Unicode da senha, apelido, navegação voltar/sucesso, erro preservando dados não secretos e descarte de senha ao abandonar; fixtures HTTP seguem contrato. (FR-001–007/010/014)
- [X] T022 [US1] Criar `src/frontend/src/app/auth/registration-validation.ts` e `registration-form.component.ts` com validação e campos de T005–006, critérios de senha visíveis, senha oculta, labels/erros associados e estado não secreto somente em memória; não adicionar limites de negócio nem persistir perfil/senha no navegador. (T021; FR-003–007/014)
- [X] T023 [US1] Criar `src/frontend/src/app/auth/registration.service.ts` e `registration.component.ts`, integrar Criar conta/Voltar em `src/frontend/src/app/auth/login.component.ts` e `/register` em `src/frontend/src/app/app.routes.ts`; consultar disponibilidade, tratar conflito/falha/resposta perdida, renovar CSRF quando necessário e confirmar sucesso antes do retorno ao login sem transportar senha. (T020/T022; FR-001–002/008/010/014)
- [X] T024 [US1] Retirar criação de titular via `src/backend/FinanceManager.Api/Commands/ProvisionOwnerCommand.cs` e caminho correspondente em `src/backend/FinanceManager.Api/Program.cs`, orientando cadastro web; atualizar usos de provisionamento em `src/backend/FinanceManager.Application/Authentication/OwnerAccessService.cs` e fixtures, sem permitir bypass de perfil obrigatório ou impedir preparação legada de T002. (FR-003/008/015)
- [X] T025 [US1] Executar testes T015–017/T021 e roteiro de cadastro de `specs/004-user-registration/quickstart.md`; registrar evidências FR-001–008/010/014 e SC-002/004 em `specs/004-user-registration/validation.md`, incluindo ausência de autenticação automática e persistência após reinício. Marcar tarefas somente após validação passar.

**Checkpoint**: US1 funciona isoladamente em instalação vazia. MVP de cadastro demonstrável; uso financeiro completo depende também de US2.

## Phase 4: User Story 2 — Entrar com e-mail e senha (P1)

**Goal**: autenticar exclusivamente por e-mail e preservar navegação, titularidade, saída e expiração.
**Independent Test**: preparar titular concluído pela fixture (sem executar cadastro no teste); verificar login correto/incorreto, caixa/trim do e-mail, destino com zero contas/contas existentes, sessão válida ao reabrir, logout e trinta minutos sem atividade.

### Tests for User Story 2

- [X] T026 [P] [US2] Evoluir `tests/FinanceManager.UnitTests/Authentication/OwnerAccessServiceTests.cs` para login por e-mail somente em titular concluído, senha exata, sessão nova/reuso, falha sem sessão e expiração/saída; impedir uso de nome completo/apelido/nome legado como alias. (FR-005/007/009/012)
- [X] T027 [P] [US2] Evoluir `tests/FinanceManager.IntegrationTests/Authentication/AuthContractTests.cs` e `OwnerSessionPersistenceTests.cs` para corpo email/password, sessão `{email}`, cookie seguro, mensagem genérica, rejeição do DTO username, bloqueio de cinco falhas/cinco minutos, dez pedidos/minuto, CSRF, logout/replay e limites de trinta minutos. (FR-009/012–013; SC-003/006)
- [X] T028 [P] [US2] Criar `src/frontend/src/app/auth/login.component.spec.ts` e `auth.service.spec.ts` para restauração, login sem sessão, credenciais incorretas, acesso financeiro bloqueado, expiração inclusive `/api/bills`, sessão válida ao reabrir e destinos MOV-05 com zero/ativas/somente arquivadas. (FR-001/009/011–014)

### Implementation for User Story 2

- [X] T029 [US2] Evoluir `src/backend/FinanceManager.Infrastructure/Authentication/IdentityOwnerAdapter.cs` e `src/backend/FinanceManager.Application/Authentication/IOwnerIdentity.cs` para busca exclusiva por e-mail normalizado de concluídos, normalização T005 e verificação exata de senha com bloqueio existente; não aplicar nova política à senha durante autenticação. (T026–027; FR-005/007/009/013)
- [X] T030 [US2] Evoluir `src/backend/FinanceManager.Application/Authentication/OwnerAccessService.cs` e `src/backend/FinanceManager.Infrastructure/Authentication/SqlOwnerSessionStore.cs` para criar/reusar sessão somente de cadastro concluído, revalidando estado no servidor e preservando saída/revogação/inatividade. (T014/T029; FR-009/012)
- [X] T031 [US2] Atualizar login/session/logout em `src/backend/FinanceManager.Api/Controllers/AuthController.cs` para contrato email/password e `{email}`, mensagens genéricas em português, CSRF e 429 preservados; manter endpoints dependentes dos serviços de aplicação, inclusive leitura da sessão. (T027/T030; FR-009/012–014)
- [X] T032 [US2] Atualizar `src/frontend/src/app/auth/auth.service.ts` para DTO por e-mail, restauração e guards financeiros; renovar CSRF após login/logout, cobrir `/api/bills` no interceptor e separar erros de transição de expiração financeira; ajustar consumidores de `username` em `src/frontend/src/main.ts` sem exibir nome técnico. (T028/T031; FR-001/009/012–014)
- [X] T033 [US2] Atualizar `src/frontend/src/app/auth/login.component.ts` e navegação em `src/frontend/src/app/app.routes.ts` para e-mail/senha, senha descartada após envio e destino MOV-05 existente; preservar sessão válida ao reabrir e oferecer Criar conta. (T032; FR-001–002/009/011)
- [X] T034 [US2] Adaptar `tests/FinanceManager.IntegrationTests/Authentication/AuthTestClient.cs`, `tests/FinanceManager.IntegrationTests/Fixtures/FinanceManagerApiFactory.cs` e consumidores de provisionamento/DTO antigo sob `tests/FinanceManager.IntegrationTests/` para titular concluído sintético por padrão; preservar testes negativos e fixtures legadas específicas, sem relaxar singleton/titularidade. (T024/T031; regressões FND-01/MOV-05/PAG-01–05)
- [X] T035 [US2] Executar T026–028 e fluxos de login/reabertura/saída de `specs/004-user-registration/quickstart.md`; registrar em `specs/004-user-registration/validation.md` evidências FR-001/005/007/009/011–014, SC-003/006 e destinos MOV-05, sem depender da execução do cadastro dentro dos testes de login.

**Checkpoint**: US2 funciona com titular concluído preparado independentemente. US1+US2 formam incremento utilizável em instalação vazia; instalação legada ainda exige US3 antes de entrega.

## Phase 5: User Story 3 — Completar cadastro do titular existente (P1)

**Goal**: comprovar credenciais antigas e concluir perfil/novo acesso no mesmo titular, sem acesso financeiro intermediário nem perda de histórico.
**Independent Test**: migrar instalação sintética com titular/sessão/histórico; provar nome/senha atuais, concluir antes de dez minutos, entrar por e-mail e comparar IDs/vínculos/valores. Credenciais/prova/cookies antigos devem ser recusados após sucesso.

### Tests for User Story 3

- [X] T036 [P] [US3] Criar `tests/FinanceManager.UnitTests/Authentication/OwnerRegistrationTransitionServiceTests.cs` para prova válida/inválida, campos completos, autorização restrita, relógio, revogação, transação, concorrência/falha sem sucesso; verificar reutilização das regras T005–007, limites exatos de dez minutos desde VerifiedAtUtc e atraso antes da emissão sem extensão do prazo. (FR-003–007/014–017)
- [X] T037 [P] [US3] Criar `tests/FinanceManager.IntegrationTests/Authentication/RegistrationTransitionContractTests.cs` para proof/authorization/complete: DTOs/status/códigos, GET sem renovação, cookie restrito seguro, ausência de cookie financeiro, CSRF, credenciais inválidas genéricas, limitação compartilhada com login, token ausente/desconhecido/expirado/revogado, conflito e sucesso sem login automático. (FR-010/012–017)
- [X] T038 [P] [US3] Criar `tests/FinanceManager.IntegrationTests/Authentication/RegistrationTransitionPersistenceTests.cs` para duas conclusões sincronizadas, prova versus conclusão, criação de sessão versus conclusão, atraso entre comprovação e emissão (dois minutos deixam oito restantes; dez minutos impedem cookie utilizável), falha entre gravações e resposta perdida/reenvio; comprovar perfil/hash/sessões/autorizações/auditoria atômicos, um vencedor e replay de cookies antigos/autorizações de outra aba recusado. (FR-014/016–017; SC-004/006–007)
- [X] T039 [P] [US3] Criar `tests/FinanceManager.IntegrationTests/Authentication/LegacyRegistrationMigrationTests.cs` usando T002: verificar cookies anteriores bloqueados antes da conclusão e preservar Owner.Id, IDs/contagens/FKs/valores/origens de contas/movimentações/obrigações/baixas antes/depois da migração e transição; entrar por e-mail após reinício. (FR-012/015–016; SC-003/005–006)
- [X] T040 [P] [US3] Criar `src/frontend/src/app/auth/registration-completion.component.spec.ts` e `registration-transition.service.spec.ts` para ambos os caminhos de prova, rejeição/expiração, nova prova preservando dados não secretos, falha/resposta perdida, retorno ao login, senha descartada e ausência de navegação financeira com autorização restrita. (FR-010/012/014–017)

### Implementation for User Story 3

- [X] T041 [US3] Completar portas/resultados em `src/backend/FinanceManager.Application/Authentication/IOwnerRegistrationStore.cs` e métodos de prova/consulta/conclusão em `OwnerRegistrationService.cs`; verificar regras e coordenar unidade transacional com relógio injetável, sem criação de identidade/sessão financeira ou dispensa de campos. (T036; FR-003–007/014–017)
- [X] T042 [US3] Implementar prova e emissão em `src/backend/FinanceManager.Infrastructure/Persistence/SqlOwnerRegistrationStore.cs` e `src/backend/FinanceManager.Infrastructure/Authentication/IdentityOwnerAdapter.cs`: aceitar nome legado somente de incompleto, usar verificação/bloqueio Identity e token criptográfico de 32 bytes; capturar VerifiedAtUtc via TimeProvider no sucesso da comprovação, antes de aguardar bloqueio, e fixar ExpiresAtUtc = VerifiedAtUtc + dez minutos, rejeitando emissão já expirada sem cookie utilizável; sob `UPDLOCK, HOLDLOCK` revalidar estado/versão da credencial após prova, guardar só hash e instantes UTC; jamais reaplicar nova política à senha antiga nem emitir autorização válida após conclusão concorrente. (T038/T041; FR-013/015/017)
- [X] T043 [US3] Implementar conclusão em `src/backend/FinanceManager.Infrastructure/Persistence/SqlOwnerRegistrationStore.cs`: mesmo bloqueio da emissão, revalidar token/estado/versão/prazo no ponto da mutação, atualizar mesmo titular/perfil/hash/data, revogar todas as OwnerSessions/RegistrationAuthorizations e gravar RegistrationCompleted em uma transação/DbContext; rollback integral em falha/ConcurrencyStamp e sem retry que sobrescreva vencedor. (T038–039/T042; FR-014/016–017)
- [X] T044 [US3] Criar `src/backend/FinanceManager.Api/Authentication/RegistrationAuthorizationCookie.cs` para `__Host-FinanceManager-Registration`, Secure/HttpOnly/SameSite=Strict/Path=/, sem Domain e expiração absoluta igual ao SQL; sem esquema/principal financeiro, JSON/URL/storage/log do token; limpar cookie restrito e financeiro presente após commit, mantendo CSRF coerente com identidade anônima. (FR-010/012–013/016–017)
- [X] T045 [US3] Expor proof/authorization/complete em `src/backend/FinanceManager.Api/Controllers/AuthController.cs` e DTOs em `RegistrationRequests.cs`, delegando à aplicação e adaptando T044; compartilhar política login, preservar CSRF anônimo, mapear todos os erros do contrato e não revelar dados existentes nem renovar prazo em consulta. (T037/T041–044; FR-010/013–017)
- [X] T046 [US3] Criar `src/frontend/src/app/auth/registration-transition.service.ts` com prova/estado da autorização/conclusão, `expiresAtUtc` informativo e estado não secreto em memória; tratar expiração/ausência de cookie sem redirecionamento genérico, renovar CSRF quando necessário e nunca tratar autorização como sessão financeira. (T040/T045; FR-012/014–017)
- [X] T047 [US3] Criar `src/frontend/src/app/auth/registration-completion.component.ts` reutilizando `registration-form.component.ts`: pedir prova antes do formulário, preservar campos não secretos ao expirar, exigir nova prova, descartar senhas ao abandonar/sucesso e retornar ao login com confirmação somente após commit. (T046; FR-003–007/010/014–017)
- [X] T048 [US3] Integrar **Usar acesso antigo** em `src/frontend/src/app/auth/login.component.ts`, **Completar cadastro existente** em `registration.component.ts` e `/register/complete` em `src/frontend/src/app/app.routes.ts`; adaptar `auth.service.ts` para encaminhar cadastro pendente sem acesso financeiro, tratar resposta perdida via disponibilidade e não permitir reabrir cadastro concluído. (T047; FR-001–002/008/012/015–017)
- [ ] T049 [US3] Executar T036–040 e roteiro de transição de `specs/004-user-registration/quickstart.md`; registrar em `specs/004-user-registration/validation.md` comparações financeiras, provas temporais antes/no/depois do limite e evidências de concorrência/rollback/replay, cobrindo FR-015–017 e SC-003–007; encerrar somente com histórico íntegro e acesso exclusivo por e-mail.

**Checkpoint**: todos os cenários US3.1–8 passam; transição preserva identidade/histórico e impede reutilização de qualquer sessão/autorização anterior. Instalações novas e legadas podem ser validadas juntas.

## Phase 6: Polish & Cross-Cutting Concerns

**Objetivo**: evidenciar aceite completo e conformidade, sem iniciar outra feature.

- [ ] T050 [P] Criar/validar `tests/FinanceManager.IntegrationTests/Authentication/RegistrationPrivacyTests.cs` para logs/respostas sem perfil, senha/token/payload financeiro, auditoria mínima e estado público sem PII; conferir HTTPS loopback, atributos de cookies, CSRF em todas as mutações e consulta de login sem carregar histórico. Registrar resultados em `specs/004-user-registration/validation-security.md`. (FR-007/012–014; SC-006)
- [ ] T051 [P] Validar teclado/foco/labels/erros/status e layout responsivo dos componentes sob `src/frontend/src/app/auth/`, corrigindo somente falhas dentro do contrato UI; registrar evidências sem segredos em `specs/004-user-registration/validation-ui.md`. (FR-001–007/010/014/017)
- [ ] T052 Executar todas as suítes xUnit/SQL/Angular, build e `has-pending-model-changes` conforme `specs/004-user-registration/quickstart.md`; conferir regressões 001–003 e verificações da matriz em `plan.md`, registrar comandos/resultados em `specs/004-user-registration/validation.md` e corrigir falhas sem remover/enfraquecer testes. (FR-001–017; SC-002–007)
- [ ] T053 Realizar avaliação SC-001 com cinco participantes usando dados sintéticos e instalação vazia por participante, medir cadastro+login sem assistência e registrar tempos/resultados em `specs/004-user-registration/validation-usability.md`; pelo menos quatro devem concluir em até cinco minutos. Se participantes não estiverem disponíveis, manter tarefa e SC-001 pendentes, sem substituir por teste automatizado.
- [ ] T054 Revisar conformidade constitucional/DDD/MVC, endpoints via aplicação, convenções de nomes/mensagens, testes por regra, rastreabilidade e evidências finais em `specs/004-user-registration/validation.md`; atualizar apenas o progresso efetivamente validado em `specs/004-user-registration/tasks.md`. Não declarar feature concluída com SC/teste pendente, não alterar constituição/spec/plano para acomodar implementação e encerrar na 004.

## Dependencies & Execution Order

```text
Phase 1 (T001–T002)
  → Phase 2 (T003–T014)
    → US1 (T015–T025)
      → US2 (T026–T035)
        → US3 (T036–T049)
          → Phase 6 (T050–T054)
```

### Dependências entre histórias

- US1 depende da fundação; teste independente não exige autenticação definitiva.
- US2 reutiliza código/contratos da US1 e evolução dos arquivos de autenticação; testes usam titular concluído de fixture e não executam US1 como preparação do fluxo avaliado.
- US3 reutiliza formulário/regras/serviço US1 e login definitivo US2; teste independente prepara legado na migração anterior e não executa cadastro inicial.
- A ordem recomendada é serial entre histórias por compartilharem AuthController, serviços, rotas e componentes. Independência dos testes não significa ausência de dependências de implementação. Não publicar migração que bloqueie legado antes de disponibilizar US3.

### Dependências internas

- T003/T004/T007 podem iniciar após setup em arquivos distintos; T005 depende de T003, T006 de T004. T008 depende de T005–006; T009 de T007–008; T010 de T009. T011 depende de T005–010; T012/T013/T014 seguem o esquema pronto. Fundação termina somente com todos os seus testes passando; T012 valida infraestrutura genérica em controller exclusivo do host de teste, enquanto o contrato real de cadastro é testado em T015 e validado após T020, antes de T025.
- Dentro de cada história, escrever testes antes dos casos de uso correspondentes. Serviços antes de adapters/endpoints integrados, endpoints e formulário antes da navegação final; checkpoint após testes passarem. Pré-requisitos específicos aparecem nas descrições.
- T050/T051 após T049 podem ocorrer em paralelo por usarem arquivos/evidências distintos; T052 consolida correções, T053 mede o produto validado e T054 depende de todas as evidências, inclusive SC-001.

## Parallel Examples

### User Story 1

Após a fundação: T015 (contrato HTTP), T016 (SQL) e T017 (caso de uso) usam arquivos distintos e podem ser escritos em paralelo. T021 (testes Angular) pode ser preparado em paralelo ao backend T018–020; ambos dependem do contrato estabilizado. T022–024 modificam arquivos integrados e seguem ordem serial. Executar testes de cada grupo após implementação, antes de T025.

### User Story 2

Após T025: T026 (xUnit serviço), T027 (HTTP/sessão) e T028 (Angular) podem ser escritos em paralelo. T029–034 seguem ordem serial por compartilharem autenticação e fixtures. Reexecutar os três grupos antes de T035.

### User Story 3

Após T035: T036 (caso de uso), T037 (HTTP), T038 (transação), T039 (migração legada) e T040 (Angular) usam arquivos separados e podem ser escritos em paralelo. T041–048 são integrados serialmente; nenhuma emissão/conclusão fica pronta sem a validação concorrente SQL de T038. Reexecutar todos antes de T049.

Paralelismo é oportunidade de execução futura, não autorização para iniciar agentes ou implementar nesta fase de geração.

## Implementation Strategy

### MVP e entrega incremental

1. Completar setup/fundação e verificar regras, migração e barreira financeira.
2. Completar US1 e demonstrar cadastro em instalação vazia; escopo mínimo de cadastro, sem prometer uso financeiro completo.
3. Completar US2 para incremento utilizável em instalação vazia com cadastro+login e navegação financeira existente.
4. Completar US3 antes de disponibilizar atualização a instalações legadas; preservar identidade e histórico é obrigatório, não opcional no aceite da 004.
5. Executar fase transversal e avaliação humana. Parar no escopo solicitado quando uma execução futura pedir somente uma fase; não iniciar automaticamente a seguinte.

### Matriz de rastreabilidade de conclusão

| Critério | Tarefas que implementam/verificam |
|---|---|
| FR-001–002 | T015, T021, T023, T028, T032–033, T048, T051 |
| FR-003–006 | T003, T005, T008, T011–012, T015, T017–023, T036, T041, T047 |
| FR-007 | T004, T006, T011, T015, T017, T021–022, T026, T029, T036, T041, T047, T050 |
| FR-008 | T008–010, T013, T015–020, T023–025, T048 |
| FR-009 | T008, T026–031, T033, T035, T039, T049 |
| FR-010 | T015, T018, T020–023, T025, T037, T044–045, T047, T049 |
| FR-011 | T028, T033, T035, T052 |
| FR-012–013 | T009, T012, T014–015, T019, T026–032, T035, T037–039, T042–046, T048–050, T052 |
| FR-014 | T012–013, T015–025, T028, T031–032, T036–038, T040–041, T043, T045–047, T051 |
| FR-015–016 | T007–010, T013–014, T024, T036–049, T052 |
| FR-017 | T007, T009, T013, T036–038, T040–049, T051–052 |
| SC-001 | T025, T035 e medição humana T053 |
| SC-002 | T003–006, T011–012, T015, T017, T021, T025, T036, T040, T052 |
| SC-003 | T025–035, T039, T049, T052 |
| SC-004 | T010, T016–019, T025, T036–039, T042–043, T049, T052 |
| SC-005 | T010, T039, T043, T049, T052 |
| SC-006 | T014, T026–028, T035, T037–039, T049–050, T052 |
| SC-007 | T007, T036–038, T040, T043, T046–049, T052 |

## Notes

São 54 tarefas: 2 setup, 12 fundação, 11 US1, 10 US2, 14 US3 e 5 transversais. Nenhuma representa implementação ou validação já concluída. Relatórios `validation*.md` são evidências a criar durante a execução; não são gerados nem preenchidos ficticiamente por este comando. Nenhum hook de extensão configurado foi encontrado na geração.
