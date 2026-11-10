# Modelo de dados · 003 · Contas a pagar e baixas

## Bill

| Campo | Tipo e regra |
|---|---|
| `Id`, `OwnerId` | UUIDs. ID gerado no servidor e titular obtido da sessão. Chave alternativa `(OwnerId, Id)`. |
| `Description` | Texto obrigatório de até 200 caracteres após remoção de espaços das extremidades. Mesmo limite da despesa para permitir a cópia. |
| `Amount` | `decimal` no domínio e `decimal(19,2)` em SQL, em BRL. Aceitar de `0.01` a `99999999999999999.99` com até duas casas, sem arredondar entrada inválida. |
| `DueDate` | `DateOnly` e SQL `date`. Preservar a data civil. |
| `CreatedAtUtc`, `UpdatedAtUtc`, `CanceledAtUtc` | Instantes UTC. `CanceledAtUtc` nulo até o cancelamento definitivo. |
| `Version` | `rowversion` do SQL Server. A ETag do detalhe combina a revisão com a situação calculada. |

A obrigação começa em aberto. Quando existe baixa ativa, fica paga. O cancelamento só é permitido em aberto e preserva a obrigação no histórico. Sem cancelamento ou baixa ativa, classificar a data como a vencer, vence hoje ou vencida em relação ao dia civil de America/Sao_Paulo. Calcular o dia uma vez por operação com o relógio injetado. A reversão restaura o cálculo pela data. Indexar `(OwnerId, DueDate, Id)` para a consulta.

## BillSettlement

| Campo | Tipo e regra |
|---|---|
| `Id`, `OwnerId` | UUIDs. Chave alternativa `(OwnerId, Id)`. |
| `BillId`, `TransactionId` | UUIDs. Referências compostas `(OwnerId, BillId)` e `(OwnerId, TransactionId)` para obrigação e movimentação, com exclusão restrita. |
| `Origin` | `LinkedExpense` para despesa existente ou `CreatedExpense` para despesa criada na baixa. Imutável. |
| `CreatedAtUtc`, `ReversedAtUtc` | Instantes UTC. `ReversedAtUtc` nulo identifica a baixa ativa. Uma baixa revertida permanece no histórico e não pode ser reativada. |

Aplicar dois índices únicos filtrados por `ReversedAtUtc IS NULL`, um em `(OwnerId, BillId)` e outro em `(OwnerId, TransactionId)`. A despesa deve estar ativa, ser do mesmo titular e ter o valor integral. No vínculo, a conta original pode estar arquivada. Na criação, a conta deve estar ativa até o commit e a data deve ser até hoje. Copiar valor e descrição da obrigação.

A baixa e a reversão atualizam condicionalmente as revisões da obrigação e da movimentação existente, ainda que seus campos financeiros continuem iguais. A despesa nova já recebe a revisão inicial na criação. Nas edições e nos cancelamentos, validar revisão e ausência de baixa ativa como parte da mesma gravação. A reversão de `CreatedExpense` cancela a movimentação. A de `LinkedExpense` preserva a movimentação.

## BillSettlementRequest

Chave primária `(OwnerId, IdempotencyKey)`, com chave de tentativa UUID não vazia. Associar por referência composta à baixa. Guardar SHA-256 de 32 bytes da entrada normalizada e os dados imutáveis do recibo, como IDs, origem e instante de criação, diretamente ou pela referência à baixa. O recibo continua disponível após a reversão.

A impressão digital usa codificação determinística e versionada de obrigação, modo, versões e dados aplicáveis. No vínculo inclui ID e revisão da despesa. Na criação inclui ID da conta e data ISO. A mesma chave com outra impressão digital retorna conflito. Após falha que possa decorrer de concorrência, concluir o rollback antes de reler o recibo em novo contexto.

## Evolução de FinancialTransaction e auditoria

- Manter as movimentações manuais existentes e acrescentar `BillSettlement` ao domínio de origens. A origem manual exige `CreationRequestId` e `CreationFingerprint`. A origem de baixa mantém ambos nulos. Ajustar a restrição de origem e o índice único de cadastro manual da 002 para incluir somente registros com chave de criação.
- Registrar eventos de obrigação criada, editada, cancelada, baixada e revertida com titular, IDs afetados e instante UTC. Reutilizar o evento de criação ou cancelamento da movimentação quando ela for criada ou cancelada pela baixa. Cada evento é persistido com a mutação correspondente. Excluir valores, descrições e conteúdo do pedido da auditoria e dos logs. Reenvios reconhecidos e operações rejeitadas não geram novo evento.
