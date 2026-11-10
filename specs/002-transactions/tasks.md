---
description: "Tarefas de implementação da feature 002-transactions"
---

# 002 · Tarefas · Movimentações manuais

**Entradas** · [Especificação](spec.md), [plano](plan.md), [pesquisa](research.md), [modelo de dados](data-model.md), [contrato HTTP](contracts/api.md) e [guia de validação](quickstart.md).

**Convenções** · Caminhos relativos à raiz. `[P]` identifica tarefas que podem ser executadas em paralelo com as indicadas nos exemplos, depois das dependências. `[US1]` e `[US2]` identificam as histórias. A [constituição](../../.specify/memory/constitution.md) exige testes xUnit das regras e casos de uso, integração em SQL Server e verificação pela interface e API. Escreva os testes de cada fase antes da implementação correspondente e confirme que detectam o comportamento ausente.

## Fase 1 · Preparação

- [ ] T001 Verificar a fundação 001 concluída e preparar bases SQL sintéticas separadas para testes e navegação conforme `specs/001-financial-foundation/quickstart.md`. Restaurar `FinanceManager.slnx` em modo bloqueado e as dependências Angular conforme `src/frontend/package-lock.json`, reutilizando a sessão, o antiforgery e o relógio já configurados.

## Fase 2 · Fundação compartilhada

- [ ] T002 Escrever testes xUnit em `tests/FinanceManager.UnitTests/Transactions/FinancialTransactionTests.cs` para natureza, valor mínimo e máximo, zero, negativo, excesso de casas, descrição vazia e limites de comprimento, datas civis, preservação da data, alteração de movimentação ativa e cancelamento definitivo. Exercitar os invariantes e seus limites conforme `specs/002-transactions/data-model.md`. Depende de T001. MOV-01, MOV-03.
- [ ] T003 Modelar a entidade em `src/backend/FinanceManager.Domain/Transactions/FinancialTransaction.cs`. Aplicar “UUID gerado no servidor. Chave primária.”, “`Income` ou `Expense`. Define o sinal no líquido.”, “Aceitar de `0.01` a `99999999999999999.99`, com até duas casas decimais e sem arredondar entrada inválida. Moeda BRL.”, “Preservar a data civil informada e rejeitar datas posteriores ao dia atual em São Paulo.”, “Texto obrigatório de até 200 caracteres após remoção de espaços das extremidades.”, “Origem `Manual`. Estado inicial `Active`, com transição definitiva para `Canceled`.” e “Instantes UTC do relógio injetado. A data de cancelamento existe apenas no estado `Canceled`.” Usar `decimal` e `DateOnly`, receber o dia civil e instantes UTC da aplicação e bloquear alterações após cancelamento. Depende de T002. MOV-01, MOV-03.
- [ ] T004 Mapear a entidade, auditoria e restrições em `src/backend/FinanceManager.Infrastructure/Persistence/FinancialTransactionConfiguration.cs`, `src/backend/FinanceManager.Infrastructure/Persistence/FinanceManagerDbContext.cs` e `src/backend/FinanceManager.Infrastructure/Persistence/FinancialAccountConfiguration.cs`. Aplicar `decimal(19,2)`, SQL `date`, “UUIDs obrigatórios. O titular vem da sessão. A referência composta aponta para `FinancialAccount(OwnerId, Id)`.”, “UUID não vazio e SHA-256 de 32 bytes. Obrigatórios para origem manual e imutáveis após a criação.” e “`rowversion` do SQL Server, exposta como ETag opaca.” Adicionar chaves alternativas por titular e ID, exclusão restrita, “Índice único em `(OwnerId, CreationRequestId)`.” e “Índice de histórico em `(OwnerId, TransactionDate DESC, Id DESC)`.” Mapear a auditoria com ID, titular, movimentação, ação e instante UTC. Depende de T003. MOV-01–04.
- [ ] T005 Gerar a migração `AddFinancialTransactions` e o snapshot em `src/backend/FinanceManager.Infrastructure/Persistence/Migrations/FinanceManagerDbContextModelSnapshot.cs`. Verificar em `tests/FinanceManager.IntegrationTests/Transactions/TransactionMigrationTests.cs` a aplicação em banco SQL Server novo e em banco com a fundação aplicada, a FK composta e as restrições, incluindo a rejeição de referência a conta de outro titular. Depende de T004. MOV-01–04.

## Fase 3 · US1 · Registrar e corrigir movimentações (P1)

**Objetivo** · Registrar, corrigir e cancelar uma movimentação manual com segurança em reenvios e alterações concorrentes.

**Teste independente** · Com conta ativa e sessão válida, registrar uma despesa, repetir a tentativa e conferir o mesmo ID, corrigir seu valor e cancelar. Consultar o detalhe para verificar o valor corrigido e o estado **Cancelada**. A tentativa de editar novamente deve ser rejeitada.

### Testes

- [ ] T006 [P] [US1] Escrever testes xUnit de casos de uso em `tests/FinanceManager.UnitTests/Transactions/TransactionCommandServiceTests.cs`. Cobrir titularidade da conta e da movimentação, dia civil de São Paulo com relógio controlado, manutenção da conta original arquivada, troca somente para conta ativa, revisão obsoleta, cancelamento e comparação da impressão digital antes das validações que dependem do estado. Incluir equivalência de `12`, `12.0` e `12.00`. Depende de T005. MOV-01, MOV-03.
- [ ] T007 [P] [US1] Escrever testes de contrato xUnit em `tests/FinanceManager.IntegrationTests/Transactions/TransactionCommandContractTests.cs` para `POST /api/transactions`, `GET` e `PUT /api/transactions/{id}` e `PUT /api/transactions/{id}/status`. Cobrir formatos e limites, sessão, antiforgery, ausência de cache, titularidade, `201` e `200` com `Location` e ETag, chave inválida ou reutilizada com conteúdo diferente, `If-Match` ausente ou inválido, revisão obsoleta, conta arquivada e alteração de cancelada. Conferir os códigos e mensagens em português de `specs/002-transactions/contracts/api.md`. Depende de T005. MOV-01, MOV-03.
- [ ] T008 [P] [US1] Escrever testes xUnit com SQL Server em `tests/FinanceManager.IntegrationTests/Transactions/TransactionCommandPersistenceTests.cs`. Cobrir resposta descartada após commit, reenvio após edição, cancelamento ou arquivamento, mesma chave em envios concorrentes, mesmo corpo com nova chave, corrida com arquivamento ao criar ou trocar a conta, escritas concorrentes com a mesma revisão e rollback de alteração e auditoria. Confirmar uma única criação e auditoria em reenvios. Depende de T005. MOV-01, MOV-03.

### Implementação

- [ ] T009 [US1] Implementar detalhe, cadastro, correção e cancelamento em `src/backend/FinanceManager.Application/Transactions/TransactionCommandService.cs` e definir a porta em `src/backend/FinanceManager.Application/Transactions/ITransactionCommandRepository.cs`. Validar titularidade e dados no servidor, derivar o dia civil pelo `TimeProvider` e calcular a impressão digital a partir dos campos normalizados conforme o modelo. Resolver reenvio antes das regras que dependem de estado mutável. Aplicar “É permitido manter a conta original arquivada. Para trocar de conta, o destino precisa estar ativo e pertencer ao titular.” Depende de T006, T007 e T008. MOV-01, MOV-03.
- [ ] T010 [US1] Implementar a porta de escrita e detalhe em `src/backend/FinanceManager.Infrastructure/Persistence/SqlTransactionCommandRepository.cs` e registrar serviço e porta em `src/backend/FinanceManager.Infrastructure/DependencyInjection.cs`. Proteger conta ativa com transação curta `REPEATABLE READ` ao criar ou trocar de conta, gravar alteração e auditoria atomicamente, usar a revisão esperada nas mutações e, após violação do índice de tentativa, encerrar a transação e reler em contexto limpo pelo titular e chave. Depende de T009. MOV-01, MOV-03.
- [ ] T011 [US1] Expor cadastro, detalhe, correção e cancelamento em `src/backend/FinanceManager.Api/Controllers/TransactionsController.cs`. Delegar ao serviço de aplicação, seguir o contrato de chave e ETag forte com `If-Match`, reutilizar o tratamento de erros da fundação e manter autenticação, CSRF e `no-store`. Depende de T010. MOV-01, MOV-03.
- [ ] T012 [P] [US1] Implementar o cliente HTTP de cadastro, detalhe e mutações em `src/frontend/src/app/transactions/transaction-command-api.service.ts`. Usar URLs relativas, strings monetárias e datas civis. Preservar `Idempotency-Key` e enviar a ETag do detalhe em `If-Match`. Depende de T007. MOV-01, MOV-03.
- [ ] T013 [US1] Criar formulário e detalhe autenticados em `src/frontend/src/app/transactions/transaction-form.component.ts` e `src/frontend/src/app/app.routes.ts`. Selecionar conta ativa no cadastro, permitir manter a conta arquivada original na edição, manter chave e corpo em memória para reenvio de resultado incerto e gerar nova chave para novo lançamento intencional. Após criar, abrir o detalhe pelo ID devolvido para corrigir ou cancelar. Exibir estado cancelado e orientar recarga após `412`. Depende de T011 e T012. MOV-01, MOV-03.
- [ ] T014 [US1] Executar a verificação independente pela tela e API de acordo com `specs/002-transactions/quickstart.md` e passar as suítes de `tests/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj` e `tests/FinanceManager.IntegrationTests/FinanceManager.IntegrationTests.csproj`. Confirmar que reenvio, edição e cancelamento funcionam a partir do detalhe. Depende de T013. MOV-01, MOV-03.

## Fase 4 · US2 · Consultar histórico e totais (P2)

**Objetivo** · Consultar histórico paginado e totais do filtro completo, abrindo no mês civil atual.

**Teste independente** · Sem usar o formulário da US1, semear dados SQL sintéticos de duas contas, com registros ativos, cancelados e datas repetidas. Abrir o histórico, conferir o mês de São Paulo, aplicar filtros e passar de página. A lista deve incluir cancelamentos e os totais devem refletir apenas registros ativos em todo o filtro.

### Testes

- [ ] T015 [P] [US2] Escrever testes xUnit em `tests/FinanceManager.UnitTests/Transactions/TransactionHistoryServiceTests.cs` para mês padrão e virada do mês em São Paulo com relógio controlado, datas inclusivas, filtros parciais ou invertidos, conta de outro titular, natureza e limites da paginação. Depende de T005. MOV-02, MOV-04.
- [ ] T016 [P] [US2] Escrever testes de contrato xUnit em `tests/FinanceManager.IntegrationTests/Transactions/TransactionHistoryContractTests.cs` para `GET /api/transactions`, com sessão e ausência de cache, filtros e erros conforme o contrato, datas efetivas, conta arquivada, inclusão de canceladas, página vazia com totais e strings monetárias com duas casas. Semear registros sem usar o formulário da US1. Depende de T005. MOV-02, MOV-04.
- [ ] T017 [P] [US2] Escrever testes xUnit com SQL Server em `tests/FinanceManager.IntegrationTests/Transactions/TransactionHistoryPersistenceTests.cs` para ordenação por data e ID, limites inclusivos, isolamento por titular, totais iguais em páginas do mesmo filtro, zero sem ativos, líquido negativo, centavos exatos, total acima do limite individual e consistência da página, contagem e totais sob alteração concorrente. Depende de T005. MOV-02, MOV-04.

### Implementação

- [ ] T018 [US2] Implementar normalização dos filtros e paginação em `src/backend/FinanceManager.Application/Transactions/TransactionHistoryService.cs` e definir a porta em `src/backend/FinanceManager.Application/Transactions/ITransactionHistoryRepository.cs`. Derivar o período inclusivo do primeiro ao último dia do mês com uma leitura do relógio em America/Sao_Paulo quando ambas as datas forem omitidas. Validar filtros, titularidade e deslocamento, com 25 itens por padrão e máximo de 100. Depende de T015, T016 e T017. MOV-02, MOV-04.
- [ ] T019 [US2] Implementar consulta e agregados em `src/backend/FinanceManager.Infrastructure/Persistence/SqlTransactionHistoryRepository.cs` e registrar serviço e porta em `src/backend/FinanceManager.Infrastructure/DependencyInjection.cs`. Usar filtro compartilhado, ordem por data e ID decrescentes e transação curta `SERIALIZABLE` para página, contagem e totais. Incluir canceladas na página e contagem. Calcular no SQL somas `decimal(38,2)` apenas dos ativos e líquido pela soma com sinal, projetando strings de duas casas sem estreitar a precisão. Depende de T018 e T010 pela edição do registro compartilhado de serviços. MOV-02, MOV-04.
- [ ] T020 [US2] Expor o histórico em `src/backend/FinanceManager.Api/Controllers/TransactionHistoryController.cs` conforme `specs/002-transactions/contracts/api.md`. Delegar à aplicação, devolver filtros efetivos, página, contagem e totais, manter sessão e `no-store` e mapear os erros para Problem Details em português. Depende de T019. MOV-02, MOV-04.
- [ ] T021 [P] [US2] Criar cliente de consulta e tela de histórico em `src/frontend/src/app/transactions/transaction-history-api.service.ts` e `src/frontend/src/app/transactions/transaction-history.component.ts`. Exibir mês inicial, filtros por conta, datas e natureza, carregamento, vazio e erro, paginação, status **Cancelada** e totais em BRL sem converter os valores em `Number`. Manter o período efetivo entre páginas e reiniciar na primeira página ao mudar filtros. Depende de T016. MOV-02, MOV-04.
- [ ] T022 [US2] Integrar a navegação autenticada em `src/frontend/src/app/app.routes.ts` e as ações de cadastro e detalhe em `src/frontend/src/app/transactions/transaction-history.component.ts`. Validar pela API e pelo navegador com dados semeados no SQL Server conforme `specs/002-transactions/quickstart.md`, incluindo filtro com paginação e totais, conta arquivada e canceladas. Depende de T013, T020 e T021. MOV-02, MOV-04.

## Fase 5 · Validação transversal

- [ ] T023 Executar as suítes xUnit, o build Angular e o fluxo completo de `specs/002-transactions/quickstart.md`. Conferir em `src/backend/FinanceManager.Infrastructure/Persistence/SqlTransactionHistoryRepository.cs`, com plano de execução SQL e histórico sintético, que o banco pagina e agrega sem materializar todo o histórico na aplicação. Revisar titularidade, mensagens em português, ausência de dados financeiros nos logs, migração e rastreabilidade dos testes conforme `specs/002-transactions/plan.md`. Depende de T014 e T022. MOV-01–04.

## Dependências e ordem de execução

`Preparação (T001) → Fundação (T002–T005) → US1 (T006–T014) → US2 (T015–T022) → Validação (T023)`.

- A fundação 001 fornece as contas, a sessão e a infraestrutura de execução. Depois de T005, as histórias podem iniciar seus testes e componentes independentes.
- US2 pode ser verificada com dados semeados, sem o formulário da US1. Serializar as edições do registro de serviços (T010 antes de T019) e da navegação (T013 antes de T022).
- A feature 003 depende da conclusão de T023. Ela entrega a proteção de MOV-03 para baixa vinculada e verifica o avanço da revisão da movimentação nas baixas e reversões.

## Exemplos de execução paralela

| História | Oportunidade |
|---|---|
| US1 | Após T005, T006, T007 e T008 tratam arquivos de teste distintos. Após T007, T012 pode avançar enquanto prosseguem os testes e a implementação do backend em arquivos distintos. |
| US2 | Após T005, T015, T016 e T017 tratam arquivos de teste distintos. Após T016, T021 pode avançar enquanto prosseguem os testes e a implementação do backend em arquivos distintos. |

## Estratégia de implementação

1. Entregar a US1 como MVP após T014. Demonstrar cadastro com reenvio, correção e cancelamento pelo detalhe.
2. Acrescentar US2 e demonstrar histórico e totais com dados semeados e com movimentações criadas pela interface.
3. Encerrar em T023 após a verificação cruzada pela API, navegador e SQL Server.
