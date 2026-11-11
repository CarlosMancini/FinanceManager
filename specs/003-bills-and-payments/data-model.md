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

A obrigação referencia `Owner` por `OwnerId`, com exclusão restrita. A obrigação começa em aberto. Quando existe baixa ativa, fica paga. O cancelamento só é permitido em aberto e preserva a obrigação no histórico. Sem cancelamento ou baixa ativa, classificar a data como a vencer, vence hoje ou vencida em relação ao dia civil de America/Sao_Paulo. Calcular o dia uma vez por operação com o relógio injetado. A reversão restaura o cálculo pela data. Indexar `(OwnerId, DueDate, Id)` para a consulta. Aplicar restrição SQL para `CanceledAtUtc` nulo em obrigação ativa e preenchido em obrigação cancelada.

## BillSettlement

| Campo | Tipo e regra |
|---|---|
| `Id`, `OwnerId` | UUIDs. Chave alternativa `(OwnerId, Id)`. |
| `BillId`, `TransactionId` | UUIDs. Referências compostas `(OwnerId, BillId)` e `(OwnerId, TransactionId)` para obrigação e movimentação, com exclusão restrita. |
| `Origin` | `LinkedExpense` para despesa existente ou `CreatedExpense` para despesa criada na baixa. Imutável. |
| `CreatedAtUtc`, `ReversedAtUtc` | Instantes UTC. `ReversedAtUtc` nulo identifica a baixa ativa. Uma baixa revertida permanece no histórico e não pode ser reativada. |

Aplicar dois índices únicos filtrados por `ReversedAtUtc IS NULL`, um em `(OwnerId, BillId)` e outro em `(OwnerId, TransactionId)`. A despesa deve estar ativa, ser do mesmo titular e ter o valor integral. No vínculo, a conta original pode estar arquivada. Na criação, a conta deve estar ativa até o commit e a data deve ser até hoje. Copiar valor e descrição da obrigação.

Restringir `Origin` em SQL a `LinkedExpense` ou `CreatedExpense`. Exigir `ReversedAtUtc` nulo na baixa ativa; depois de preenchido, o vínculo é histórico e não pode voltar a ativo.

A baixa e a reversão atualizam condicionalmente as revisões da obrigação e da movimentação existente, ainda que seus campos financeiros continuem iguais. A despesa nova já recebe a revisão inicial na criação. Nas edições e nos cancelamentos, validar revisão e ausência de baixa ativa como parte da mesma gravação. A reversão de `CreatedExpense` cancela a movimentação. A de `LinkedExpense` preserva a movimentação.

## BillSettlementRequest

Chave primária `(OwnerId, IdempotencyKey)`, com chave de tentativa UUID não vazia. Associar por referências compostas ao titular e à baixa, ambas com exclusão restrita. Guardar SHA-256 de 32 bytes da entrada normalizada e os dados imutáveis do recibo, como IDs, origem e instante de criação, diretamente ou pela referência à baixa. O recibo continua disponível após a reversão.

A impressão digital usa codificação determinística e versionada de obrigação, modo, tokens de versão recebidos no pedido e dados aplicáveis; não usa revisões correntes relidas após o commit. No vínculo inclui ID e token de versão da despesa. Na criação inclui ID da conta e data ISO. A mesma chave com outra impressão digital retorna conflito. Após falha que possa decorrer de concorrência, concluir o rollback antes de reler o recibo em novo contexto.

## Evolução de FinancialTransaction e auditoria

- Manter as movimentações manuais existentes e acrescentar `BillSettlement` ao domínio de origens. Ampliar o tamanho da coluna `Source` e sua restrição SQL para o novo valor. A origem manual exige `CreationRequestId` e `CreationFingerprint`; torná-los anuláveis em SQL, mas exigir que estejam ambos preenchidos para origem manual e ambos nulos para origem de baixa. Substituir o índice único da 002 por índice filtrado em `(OwnerId, CreationRequestId)` somente para registros com chave de criação.
- Registrar eventos de obrigação criada, editada, cancelada, baixada e revertida com titular, IDs afetados e instante UTC. Reutilizar o evento de criação ou cancelamento da movimentação quando ela for criada ou cancelada pela baixa. Cada evento é persistido com a mutação correspondente. Excluir valores, descrições e conteúdo do pedido da auditoria e dos logs. Reenvios reconhecidos e operações rejeitadas não geram novo evento.

## BillAudit

| Campo | Tipo e regra |
|---|---|
| `Id` | Inteiro longo gerado pelo banco. |
| `OwnerId`, `BillId` | UUIDs obrigatórios. Referência composta à obrigação do mesmo titular, com exclusão restrita. |
| `SettlementId`, `TransactionId` | UUIDs opcionais preenchidos quando a ação envolve baixa ou reversão. Devem pertencer ao mesmo titular. |
| `Action` | `Created`, `Edited`, `Canceled`, `Settled` ou `Reversed`, restringido em SQL. |
| `OccurredAtUtc` | Instante UTC obrigatório. |

Os eventos são somente de acréscimo, escopados pelo titular e sem valores, descrições ou corpo do pedido.
