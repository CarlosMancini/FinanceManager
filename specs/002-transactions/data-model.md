# Modelo de dados · 002 · Movimentações manuais

## FinancialTransaction

| Campo | Tipo e regra |
|---|---|
| `Id` | UUID gerado no servidor. Chave primária. |
| `OwnerId`, `AccountId` | UUIDs obrigatórios. O titular vem da sessão. A referência composta aponta para `FinancialAccount(OwnerId, Id)`. |
| `Kind` | `Income` ou `Expense`. Define o sinal no líquido. |
| `Amount` | `decimal(19,2)` no SQL Server e `decimal` no domínio. Aceitar de `0.01` a `99999999999999999.99`, com até duas casas decimais e sem arredondar entrada inválida. Moeda BRL. |
| `TransactionDate` | `DateOnly` e SQL `date`. Preservar a data civil informada e rejeitar datas posteriores ao dia atual em São Paulo. |
| `Description` | Texto obrigatório de até 200 caracteres após remoção de espaços das extremidades. |
| `Source`, `Status` | Origem `Manual`. Estado inicial `Active`, com transição definitiva para `Canceled`. |
| `CreationRequestId`, `CreationFingerprint` | UUID não vazio e SHA-256 de 32 bytes. Obrigatórios para origem manual e imutáveis após a criação. |
| `CreatedAtUtc`, `UpdatedAtUtc`, `CanceledAtUtc` | Instantes UTC do relógio injetado. A data de cancelamento existe apenas no estado `Canceled`. |
| `Version` | `rowversion` do SQL Server, exposta como ETag opaca. |

### Vínculos e restrições

- A migração acrescenta chave alternativa `(OwnerId, Id)` à conta e à movimentação para referências com titularidade preservada. A conta referenciada tem exclusão restrita.
- Índice único em `(OwnerId, CreationRequestId)`. Índice de histórico em `(OwnerId, TransactionDate DESC, Id DESC)`. Aplicar restrições SQL aos domínios de natureza, origem, estado e valor positivo.
- A impressão digital usa codificação estruturada determinística e versionada dos campos normalizados de criação. Inclui UUID da conta, natureza, valor com duas casas em formato invariante, data ISO e descrição normalizada. Valores como `12`, `12.0` e `12.00` produzem a mesma impressão digital para os mesmos demais campos.
- A validação estrutural e a comparação do pedido antecedem as regras dependentes de estado em um reenvio. O reenvio de uma criação gravada encontra a mesma movimentação mesmo que ela tenha mudado ou sua conta tenha sido arquivada. Preservar chave e impressão digital durante correção e cancelamento.

### Ciclo de vida

- **Criação** · conta ativa do titular, natureza válida, valor estritamente positivo (`0.01` mínimo), data válida e descrição preenchida. Ausência de qualquer campo ou valor zero/negativo não cria movimentação; a natureza determina o sinal do total.
- **Correção** · movimentação ativa, revisão atual e dados válidos. É permitido manter a conta original arquivada. Para trocar de conta, o destino precisa estar ativo e pertencer ao titular.
- **Cancelamento** · movimentação ativa e revisão atual. O registro permanece no histórico e sai dos agregados. O cancelamento é definitivo e bloqueia edição e conciliação.
- **Baixa vinculada** · na feature 003, uma baixa ativa bloqueia correção e cancelamento até sua reversão. O vínculo deve preservar a titularidade e participar da mesma verificação de concorrência.

## FinancialTransactionAudit

Evento com ID próprio, ID da movimentação, ID do titular, ação `Created`, `Edited` ou `Canceled` e instante UTC. Registrar no mesmo commit da mudança. Reenvio reconhecido, rejeição e rollback não geram evento. A auditoria não copia valores, descrições ou conteúdo do pedido.
