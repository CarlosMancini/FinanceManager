---
description: "Tarefas de implementação da feature 003-bills-and-payments"
---

# 003 · Tarefas · Contas a pagar e baixas

**Entradas** · [Especificação](spec.md), [plano](plan.md), [pesquisa](research.md), [modelo de dados](data-model.md), [contrato HTTP](contracts/api.md) e [roteiro de validação](quickstart.md).

**Convenções** · Caminhos relativos à raiz. `[P]` indica tarefas que podem avançar em arquivos distintos após as dependências declaradas. `[US1]` e `[US2]` identificam as histórias. Escrever os testes de cada fase antes da implementação correspondente e confirmar que detectam o comportamento ausente, conforme a [constituição](../../.specify/memory/constitution.md).

## Fase 1 · Preparação

- [X] T001 Confirmar a entrega de 002/T033 em `specs/002-transactions/tasks.md`, incluindo a migração, a API, a interface, os testes e os fluxos de movimentações. Restaurar `FinanceManager.slnx` em modo bloqueado e instalar as dependências da SPA com `npm ci` conforme `src/frontend/package-lock.json`. Preparar bases SQL sintéticas separadas para navegação e testes conforme `specs/001-financial-foundation/quickstart.md`.

## Fase 2 · Fundação compartilhada

- [X] T002 Escrever testes xUnit do domínio em `tests/FinanceManager.UnitTests/Bills/BillTests.cs` para descrição vazia e limites, valor mínimo e máximo, escala, preservação da data, situações civis, correção enquanto em aberto e cancelamento definitivo e bloqueio com baixa ativa. Depende de T001. PAG-01, PAG-02, PAG-05.
- [X] T003 Modelar a obrigação em `src/backend/FinanceManager.Domain/Bills/Bill.cs`. Aplicar “UUIDs. ID gerado no servidor e titular obtido da sessão.”, “Texto obrigatório de até 200 caracteres após remoção de espaços das extremidades.”, “`decimal` no domínio e `decimal(19,2)` em SQL, em BRL. Aceitar de `0.01` a `99999999999999999.99` com até duas casas, sem arredondar entrada inválida.”, “`DateOnly` e SQL `date`. Preservar a data civil.” e “Instantes UTC. `CanceledAtUtc` nulo até o cancelamento definitivo.” Calcular a situação com o dia civil recebido da aplicação e com o vínculo ativo. Depende de T002. PAG-01, PAG-02, PAG-05.
- [X] T004 Definir os registros de vínculo e recibo em `src/backend/FinanceManager.Domain/Bills/BillSettlement.cs` e `src/backend/FinanceManager.Application/Bills/BillSettlementRequest.cs`. Mapear obrigação, vínculo, recibo e `BillAudit` em `src/backend/FinanceManager.Infrastructure/Persistence/BillConfiguration.cs`, `src/backend/FinanceManager.Infrastructure/Persistence/BillSettlementConfiguration.cs`, `src/backend/FinanceManager.Infrastructure/Persistence/BillSettlementRequestConfiguration.cs` e `src/backend/FinanceManager.Infrastructure/Persistence/FinanceManagerDbContext.cs`. Aplicar referências restritas ao titular, chaves alternativas por `(OwnerId, Id)`, `rowversion`, índice `(OwnerId, DueDate, Id)`, constraints de cancelamento, origem e reversão, e dois índices únicos filtrados por baixa ativa. Definir recibo com chave `(OwnerId, IdempotencyKey)`, referência composta à baixa, SHA-256 de 32 bytes e dados imutáveis. Mapear `BillAudit` somente de acréscimo com ação, titular, IDs e instante UTC, sem dados financeiros. Depende de T003. PAG-01–05.
- [X] T005 Evoluir origem e idempotência manual em `src/backend/FinanceManager.Domain/Transactions/FinancialTransaction.cs`, `src/backend/FinanceManager.Infrastructure/Persistence/FinancialTransactionConfiguration.cs` e `src/backend/FinanceManager.Infrastructure/Persistence/SqlTransactionCommandRepository.cs`. Acrescentar `BillSettlement`, ampliar `Source`, tornar `CreationRequestId` e `CreationFingerprint` anuláveis em SQL e exigir ambos preenchidos para origem manual ou ambos nulos para origem de baixa. Substituir o índice único da 002 por índice filtrado somente para registros com chave, preservando consulta e reenvio do cadastro manual. Depende de T004. PAG-03, PAG-05, MOV-01.
- [X] T006 Gerar a migração `AddBillsAndSettlements` em `src/backend/FinanceManager.Infrastructure/Persistence/Migrations/` e atualizar `src/backend/FinanceManager.Infrastructure/Persistence/Migrations/FinanceManagerDbContextModelSnapshot.cs`. Verificar em `tests/FinanceManager.IntegrationTests/Bills/BillMigrationTests.cs` a aplicação em banco novo e sobre a 002 com dados, as referências por titular, a unicidade de baixas ativas, a nulabilidade condicional das chaves de criação, a criação de mais de uma despesa gerada por titular e a preservação do histórico e das chaves manuais. Depende de T005. PAG-01–05, MOV-01.

## Fase 3 · US1 · Acompanhar obrigações e vencimentos (P1)

**Objetivo** · Cadastrar, corrigir, cancelar e consultar obrigações, com total em aberto coerente com os filtros.

**Teste independente** · Cadastrar obrigações vencida no mês anterior, com vencimento hoje e com vencimento amanhã. Conferir os três estados e a presença da primeira na lista inicial. Filtrar por situação e período, paginar e conferir o total do filtro inteiro. Cancelar uma obrigação em aberto e conferir sua presença no histórico e saída do total.

### Testes

- [X] T007 [P] [US1] Escrever testes xUnit em `tests/FinanceManager.UnitTests/Bills/BillServiceTests.cs` para titularidade, validação de comandos, atualização com revisão atual, conflito com revisão obsoleta, bloqueio por baixa ativa, limites de filtros e virada do dia em America/Sao_Paulo com relógio controlado. Depende de T006. PAG-01, PAG-02, PAG-05.
- [X] T008 [P] [US1] Escrever testes de contrato em `tests/FinanceManager.IntegrationTests/Bills/BillContractTests.cs` para `GET` e `POST /api/bills`, `GET` e `PUT /api/bills/{id}` e `PUT /api/bills/{id}/status`. Cobrir sessão, CSRF, titularidade, `no-store`, `Location`, ETag forte sensível à situação, precondições, formatos, filtros, paginação e Problem Details em português conforme `specs/003-bills-and-payments/contracts/api.md`. Depende de T006. PAG-01, PAG-02.
- [X] T009 [P] [US1] Escrever testes SQL Server em `tests/FinanceManager.IntegrationTests/Bills/BillPersistenceTests.cs` para ordenação em empate, vencidas de meses anteriores, datas inclusivas, total do filtro em cada página, total superior ao limite de uma obrigação, zero em filtros vazios ou só de pagas ou canceladas, versão e auditoria. Semear baixa ativa para verificar a exclusão do total e o bloqueio de escrita antes de usar os fluxos da US2. Depende de T006. PAG-01, PAG-02, PAG-05.

### Implementação

- [X] T010 [US1] Implementar cadastro, detalhe, edição, cancelamento e filtros em `src/backend/FinanceManager.Application/Bills/BillService.cs` e definir as portas em `src/backend/FinanceManager.Application/Bills/IBillRepository.cs`. Calcular o dia civil uma vez por operação com `TimeProvider`. Normalizar `open` como padrão, permitir consultar pagas, canceladas e todo o histórico, validar o par inclusivo `from`/`to` e a paginação de 25 itens por padrão e 100 no máximo. Depende de T007, T008 e T009. PAG-01, PAG-02, PAG-05.
- [X] T011 [US1] Implementar escrita condicional, auditoria, detalhe e consulta em `src/backend/FinanceManager.Infrastructure/Persistence/SqlBillRepository.cs` e registrar as dependências em `src/backend/FinanceManager.Infrastructure/DependencyInjection.cs`. Validar revisão e ausência de baixa ativa na gravação. Ler página, contagem e soma em transação curta `SERIALIZABLE`, usando `dueDate ASC, id ASC` e o filtro inteiro. Somar somente as abertas com `decimal(38,2)` e devolver string invariante com duas casas, preservando contagem e total em página vazia. Depende de T010. PAG-01, PAG-02, PAG-05.
- [X] T012 [US1] Expor os endpoints de obrigação em `src/backend/FinanceManager.Api/Controllers/BillsController.cs` e tratar a ETag forte em `src/backend/FinanceManager.Api/Http/BillVersionHeaders.cs`. Aplicar “A ETag do detalhe combina a revisão com a situação calculada.” Delegar aos serviços, reutilizar segurança da fundação e responder conforme `specs/003-bills-and-payments/contracts/api.md`. Depende de T011. PAG-01, PAG-02.
- [X] T013 [P] [US1] Implementar cliente, lista com filtros e total, detalhe e formulário em `src/frontend/src/app/bills/bill-api.service.ts`, `src/frontend/src/app/bills/bill-list.component.ts` e `src/frontend/src/app/bills/bill-detail.component.ts`. Abrir em aberto sem período, reiniciar a página ao mudar filtros, mostrar carregamento, vazio e erro, manter valores monetários como strings e orientar recarga em conflito de versão. Depende de T008. PAG-01, PAG-02.
- [X] T014 [US1] Integrar rotas autenticadas em `src/frontend/src/app/app.routes.ts` e acesso à lista em `src/frontend/src/main.ts`. Executar o teste independente pela interface e API com `specs/003-bills-and-payments/quickstart.md` e confirmar os testes da história. Depende de T012 e T013. PAG-01, PAG-02.

## Fase 4 · US2 · Dar baixa integral e reverter (P2)

**Objetivo** · Dar baixa por vínculo ou criação de despesa, repetir a tentativa com segurança e reverter mantendo os dois totais corretos.

**Teste independente** · Semear duas obrigações e uma despesa compatível de conta arquivada. Vincular essa despesa, reverter e verificar que ela permanece ativa. Dar baixa na outra obrigação criando despesa com valor e descrição copiados, repetir o pedido e conferir um único lançamento. Reverter e verificar o cancelamento da despesa gerada e o retorno da obrigação ao total em aberto.

### Testes

- [X] T015 [P] [US2] Escrever testes xUnit em `tests/FinanceManager.UnitTests/Bills/BillSettlementServiceTests.cs` para elegibilidade, divergência de valor, titularidade, versões, conta arquivada, data futura, pagamento antecipado ou atrasado, campos copiados, origem, normalização da tentativa, recibo antes de regras mutáveis e efeitos de reversão por origem. Cobrir também bloqueios e conflitos de edição e cancelamento. Depende de T006. PAG-03, PAG-04, PAG-05, MOV-03.
- [X] T016 [P] [US2] Escrever testes de contrato em `tests/FinanceManager.IntegrationTests/Bills/BillSettlementContractTests.cs` para `GET /api/bills/{id}/eligible-expenses`, `POST /api/bills/{id}/settlements` e `PUT /api/bills/{id}/settlements/{settlementId}/status`. Cobrir filtros de despesas antigas, elegibilidade, sessão, CSRF, titularidade, modos exclusivos, chave e tags inválidas, conflitos `409`, recibo `201`/`200` e reversão `204` pelo ID. Cobrir `source: "billSettlement"`, `TRANSACTION_LINKED` e revisão obsoleta nas rotas da 002. Depende de T006. PAG-03, PAG-04, PAG-05, MOV-03.
- [X] T017 [P] [US2] Escrever testes SQL Server em `tests/FinanceManager.IntegrationTests/Bills/BillSettlementPersistenceTests.cs` para baixa concorrente da mesma obrigação e da mesma despesa, reenvio concorrente, perda de resposta, reenvio após reversão, chave divergente, arquivamento concorrente, corrida com edição ou cancelamento, avanço das duas revisões, leitura de totais durante baixa e rollback com falha entre gravações. Confirmar ausência de duplicatas ou auditoria extra e reversão antiga sem efeito na nova baixa. Depende de T006. PAG-02–05, MOV-03.

### Implementação

- [X] T018 [US2] Implementar seleção, baixa e reversão em `src/backend/FinanceManager.Application/Bills/BillSettlementService.cs` e definir a porta em `src/backend/FinanceManager.Application/Bills/IBillSettlementRepository.cs`. Exigir despesa ativa, do mesmo titular, de valor integral e sem baixa ativa; permitir conta arquivada no vínculo, exigir conta ativa na criação, limitar a data a hoje e copiar valor e descrição. Criar impressão digital determinística e versionada com os tokens enviados no pedido original, não revisões correntes relidas; no vínculo incluir ID e token da despesa, e na criação incluir conta e data ISO. Consultar recibo antes de regras mutáveis. Na reversão, cancelar somente a despesa `CreatedExpense` e preservar `LinkedExpense`. Depende de T010, T015, T016 e T017. PAG-03, PAG-04, PAG-05.
- [X] T019 [US2] Implementar as transações de baixa e reversão e a consulta paginada de despesas em `src/backend/FinanceManager.Infrastructure/Persistence/SqlBillSettlementRepository.cs` e registrar a porta em `src/backend/FinanceManager.Infrastructure/DependencyInjection.cs`. Gravar vínculo, despesa gerada, recibo e auditoria atomicamente, proteger conta ativa até o commit e atualizar as duas revisões condicionalmente. Aplicar “Após falha que possa decorrer de concorrência, concluir o rollback antes de reler o recibo em novo contexto.” Reverter pelo ID da baixa, sem reativá-la. Selecionar somente despesas elegíveis e ordenar por `transactionDate DESC, id DESC`. Depende de T011 e T018. PAG-03, PAG-04, PAG-05.
- [X] T020 [US2] Integrar a proteção de movimentações em `src/backend/FinanceManager.Application/Transactions/TransactionCommandService.cs` e `src/backend/FinanceManager.Infrastructure/Persistence/SqlTransactionCommandRepository.cs`. Aplicar “Nas edições e nos cancelamentos, validar revisão e ausência de baixa ativa como parte da mesma gravação.” Preservar os demais comportamentos de edição, cancelamento e reenvio manual. Depende de T019. PAG-05, MOV-03.
- [X] T021 [US2] Expor a seleção, baixa e reversão em `src/backend/FinanceManager.Api/Controllers/BillSettlementsController.cs`, delegando ao serviço de aplicação. Aplicar os corpos exclusivos, versões, chave, recibo imutável e códigos do contrato. Atualizar `src/backend/FinanceManager.Api/Controllers/TransactionsController.cs` e `src/backend/FinanceManager.Api/Controllers/TransactionHistoryController.cs` para a origem adicional e o conflito `TRANSACTION_LINKED` quando aplicável. Depende de T019 e T020. PAG-03, PAG-04, PAG-05, MOV-03.
- [X] T022 [P] [US2] Criar o cliente HTTP de despesas elegíveis, baixa e reversão em `src/frontend/src/app/bills/bill-settlement-api.service.ts`. Preservar tags, chave e corpo no reenvio e tratar o recibo como resultado da tentativa. Depende de T016. PAG-03, PAG-04.
- [X] T023 [US2] Criar o seletor e o formulário de baixa em `src/frontend/src/app/bills/bill-settlement.component.ts`. Permitir vínculo com conta arquivada e oferecer só contas ativas na criação, mostrando valor e descrição copiados. Reenviar a mesma tentativa após resultado incerto, criar outra chave para uma nova tentativa e orientar a atualização em conflitos de versão. Depende de T022. PAG-03, PAG-05.
- [X] T024 [US2] Integrar as ações de baixa e reversão ao detalhe em `src/frontend/src/app/bills/bill-detail.component.ts`, explicando o efeito de reversão para cada origem. Recarregar detalhe, lista e total após mutação ou reenvio. Atualizar `src/frontend/src/app/transactions/transaction-form.component.ts` e `src/frontend/src/app/transactions/transaction-history.component.ts` para exibir a origem adicional e orientar a reversão ao receber `TRANSACTION_LINKED`. Depende de T014, T021 e T023. PAG-02–05, MOV-03.
- [X] T025 [US2] Executar o teste independente da US2 pelo navegador e pela API em `specs/003-bills-and-payments/quickstart.md`. Conferir recibo repetido, efeitos no histórico e nos totais e sucesso dos testes da história em `tests/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj` e `tests/FinanceManager.IntegrationTests/FinanceManager.IntegrationTests.csproj`. Depende de T024. PAG-03–05, MOV-03.

## Fase 5 · Validação transversal

- [X] T026 Executar as duas suítes xUnit, a compilação Angular por `src/frontend/package.json` e o roteiro completo em `specs/003-bills-and-payments/quickstart.md`. Conferir migrações em SQL Server novo e atualizado, regressões da 002, plano de execução com dados sintéticos e paginação e soma no banco em `src/backend/FinanceManager.Infrastructure/Persistence/SqlBillRepository.cs`. Revisar titularidade, mensagens em português, auditoria sem dados financeiros e rastreabilidade pela matriz em `specs/003-bills-and-payments/plan.md`. Depende de T014 e T025. PAG-01–05, MOV-01, MOV-03.

## Dependências e ordem de execução

### Adequação do cadastro de obrigação · 2026-11-11

As tarefas T001–T026 documentam a entrega anterior e permanecem concluídas. As tarefas novas abaixo implementam PAG-01 revisado; o cadastro reutiliza a máscara entregue por 002/T039.

#### Fase 6 · US1 · Validar e mascarar nova obrigação (P1)

**Teste independente** · Descrição, valor ou vencimento ausente impede o cadastro; zero/negativo são rejeitados sem obrigação criada. Dígitos digitados ou colados aparecem como `R$ X,XX`, e `0.01` é aceito.

- [ ] T027 [P] [US1] Acrescentar testes xUnit em `tests/FinanceManager.UnitTests/Bills/BillTests.cs` e `tests/FinanceManager.UnitTests/Bills/BillServiceTests.cs` para descrição vazia, valor `0`, negativo, mínimo `0.01` e vencimento obrigatório, identificando lacunas de PAG-01. Depende de T026.
- [ ] T028 [P] [US1] Acrescentar testes HTTP em `tests/FinanceManager.IntegrationTests/Bills/BillContractTests.cs` para `POST /api/bills` sem cada campo (`description`, `amount`, `dueDate`), nulo/vazio quando aplicável, zero e negativo; exigir `400 VALIDATION_FAILED` sem obrigação criada. Depende de T026.
- [ ] T029 [P] [US1] Criar testes de componente em `src/frontend/src/app/bills/bill-list.component.spec.ts` para três campos obrigatórios, bloqueio de valor vazio/zero, máscara `R$ X,XX`, entrada e colagem apenas numéricas e conversão exata; reutilizar os casos de `src/frontend/src/app/transactions/brl-currency.spec.ts`. Depende de 002/T039 e T026.
- [ ] T030 [US1] Corrigir apenas as lacunas reveladas por T027–T028 em `src/backend/FinanceManager.Domain/Bills/Bill.cs`, `src/backend/FinanceManager.Application/Bills/BillService.cs` e `src/backend/FinanceManager.Api/Controllers/BillsController.cs`; manter `Amount` de `0.01` a `99999999999999999.99`, até duas casas, sem arredondar entrada inválida, e exigir descrição, valor e vencimento. Depende de T027 e T028.
- [ ] T031 [US1] Atualizar `src/frontend/src/app/bills/bill-list.component.ts` para reutilizar `src/frontend/src/app/transactions/brl-currency.ts`, filtrar digitação e colagem a dígitos, exibir `R$ X,XX`, converter para decimal invariante e impedir envio sem descrição, valor e vencimento ou com valor zero. Depende de 002/T039, T029 e T030.

#### Fase 7 · Verificação da adequação

- [ ] T032 Executar testes xUnit, testes Angular, build e cenários de `specs/003-bills-and-payments/quickstart.md` pela interface e API; conferir PAG-01 e a regressão da 002, inclusive valor mínimo, campos obrigatórios e rejeição sem criação. Depende de T031 e 002/T040.

**Ordem** · T027 e T028 podem avançar em paralelo; T029 usa a máscara concluída na 002 e pode avançar com os testes de backend. T030 segue os testes de backend, T031 integra a tela, e T032 fecha a adequação. A US1 revisada é o MVP desta correção.

`002/T033 → Preparação (T001) → Fundação (T002–T006) → US1 (T007–T014) → US2 (T015–T025) → Validação (T026)`.

Após T006, os testes da US2 podem usar registros semeados e avançar antes da integração da US1. Os serviços e o repositório da US2 aproveitam T010 e T011, e a integração visual depende de T014. A US1 pode ser entregue e validada sozinha.

## Exemplos de execução paralela

| História | Oportunidade |
|---|---|
| US1 | Após T006, T007, T008 e T009 escrevem testes em arquivos distintos. Após T008, T013 pode avançar em paralelo com a implementação do backend. |
| US2 | Após T006, T015, T016 e T017 escrevem testes em arquivos distintos. Após T016, T022 pode avançar em paralelo com a implementação do backend. |

## Estratégia de implementação

1. Entregar a US1 como MVP após T014, com cadastro, correção, cancelamento, vencimentos e total em aberto filtrado.
2. Acrescentar a US2 e demonstrar as duas formas de baixa, reenvio e reversão após T025.
3. Encerrar em T026 após a validação cruzada do navegador, da API e do SQL Server.
