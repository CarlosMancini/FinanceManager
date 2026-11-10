# Modelo de dados · 001 · Fundação financeira

## Titular · Owner

- `Id` (`Guid`) é a identidade estável do titular usada nas contas e nas sessões.
- O ASP.NET Core Identity mantém nome de acesso, hash de senha e metadados de autenticação. O comando local cria um único titular de forma transacional, com proteção contra provisionamentos simultâneos. O segredo é lido por prompt protegido.
- Um titular tem zero ou mais sessões e contas. Os serviços recebem `OwnerId` do contexto autenticado e buscam contas por `OwnerId` e `Id`.

## Sessão · OwnerSession

| Campo | Regra |
|---|---|
| `Id` | Identificador aleatório de alta entropia. O cookie carrega uma referência protegida. |
| `OwnerId` | FK para o titular autenticado. |
| `CreatedAtUtc`, `LastActivityAtUtc` | Instantes UTC. Ambos começam no login. |
| `RevokedAtUtc` | Opcional. O logout registra a revogação antes de limpar o cookie. |

A sessão está ativa enquanto não houver revogação e o instante atual for anterior a `LastActivityAtUtc + 30 min`. Requisições autenticadas admitidas atualizam a atividade, com verificação de validade e atualização em uma única operação condicional no SQL Server. A atividade nunca retrocede. Uma sessão revogada ou já expirada exige novo login com novo identificador. O adaptador de tickets deriva a expiração da última atividade. O tempo de sessão usa um relógio injetável.

Uma requisição conta como atividade após autenticação, autorização da rota e antiforgery quando exigido, antes de executar o caso de uso. Ativos estáticos, emissão de token antiforgery, requisições barradas nesses controles e polling de manutenção ficam fora da contagem. Logout revoga diretamente a sessão ainda válida.

## Conta financeira · FinancialAccount

| Campo | Regra |
|---|---|
| `Id` | `Guid` estável, gerado no servidor. |
| `OwnerId` | FK obrigatória. Titular imutável. |
| `Name` | Nome de exibição obrigatório, aparado nas extremidades, de 1 a 120 unidades UTF-16. Preserva caixa e grafia. |
| `NormalizedName` | Chave obrigatória derivada por trim, NFC, caixa alta invariante e NFC. Acentos e espaços internos continuam significativos. |
| `Type` | `checking`, `savings` ou `wallet`. |
| `Currency` | `BRL`, definida no cadastro e imutável. |
| `Status` | `active` no cadastro, com transições `active → archived → active`. |
| `Version` | `rowversion` do SQL Server. Identificador opaco de revisão, exposto via ETag. |

O índice único não filtrado `UX_FinancialAccounts_OwnerId_NormalizedName` usa chave normalizada `nvarchar(450)` com collation binária `Latin1_General_100_BIN2`. A restrição vale para qualquer estado e tipo. Arquivar preserva nome, identidade e vínculo futuro com o histórico. Uma conta arquivada fica fora da seleção de novas movimentações. Saldo inicial será representado por movimentação em feature posterior.

Alterações de nome, tipo e estado comparam a revisão do cliente com a atual. Para alteração efetiva, a operação SQL também compara a revisão esperada. Uma atualização sem mudança de dados pode retornar a revisão atual após validar a precondição. Alterações efetivas registram ação, titular, ID da conta e instante UTC em um registro de auditoria gravado na mesma transação. Credenciais, tokens e valores anteriores dos campos ficam fora da auditoria.
