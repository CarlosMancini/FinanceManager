# Modelo de dados · 004

## Owner e perfil

Ampliar o `Owner` Identity existente; regras de perfil no domínio, coordenação na aplicação e armazenamento na infraestrutura.

| Campo | Tipo e regra |
|---|---|
| `Id` | Guid existente e imutável, preservado nas FKs financeiras. |
| `ProvisioningSlot` | tinyint obrigatório, check `= 1`, índice único `UX_Owners_ProvisioningSlot`. |
| `FullName` | nvarchar(max), obrigatório quando concluído; trim externo, preservar grafia. |
| `Nickname` | nvarchar(max) opcional; trim externo, vazio vira null. |
| `BirthDate` | date/DateOnly, obrigatório quando concluído; existente, não futuro em America/Sao_Paulo via relógio injetável, sem idade mínima. |
| `Email`, `NormalizedEmail` | nvarchar(max), obrigatórios quando concluído; um @ com partes não vazias, sem espaços internos; trim e caixa invariável. Comparação ordinal/collation binária coerente. |
| `PhoneNumber` | Campo Identity, 10–15 dígitos mais `+` inicial opcional; remover espaços/parênteses/hífens, rejeitar letras/outros símbolos. |
| `RegistrationCompletedAtUtc` | datetimeoffset opcional. Null = legado incompleto; preenchido = definitivo. |
| `UserName`, `NormalizedUserName` | Campos internos Identity; novos usam ID técnico, legados mantêm nome para prova somente enquanto incompletos. |
| Credencial e bloqueio | Campos Identity existentes de hash/stamps/lockout. Nunca persistir senha original. |

Campos novos permitem null no legado. Check SQL exige campos obrigatórios não vazios quando a data de conclusão estiver preenchida. Regras de nascimento futuro são avaliadas por relógio no domínio, não pelo relógio do banco. Senha obedece FR-007, inclusive 12–128 pontos de código; validadores Identity não acrescentam regras divergentes. E-mail não precisa de índice de texto ilimitado: só pode existir um titular. Remover explicitamente `EmailIndex` do modelo Identity e da migração antes de ampliar NormalizedEmail; SQL Server não aceita nvarchar(max) como chave desse índice. Manter o índice de nome interno e o de slot.

## RegistrationAuthorization

| Campo | Tipo e regra |
|---|---|
| `Id` | Guid gerado pelo servidor, chave primária. |
| `OwnerId` | FK obrigatória ao titular comprovado, exclusão restrita; não é autoridade recebida do cliente. |
| `TokenHash` | binary(32), SHA-256 de token aleatório de 32 bytes, índice único. |
| `VerifiedAtUtc`, `ExpiresAtUtc` | datetimeoffset, prazo de exatamente dez minutos desde a comprovação das credenciais antigas. |
| `RevokedAtUtc` | datetimeoffset opcional, preenchido para todas as autorizações ao concluir. |

`VerifiedAtUtc` é capturado por TimeProvider quando a verificação das credenciais antigas termina com sucesso, antes de aguardar o bloqueio transacional. `ExpiresAtUtc = VerifiedAtUtc + dez minutos`. Espera por bloqueio, persistência e envio do cookie não alteram esse instante nem reiniciam o prazo. Se o prazo já tiver terminado ao emitir a autorização, rejeitar a emissão sem entregar cookie utilizável e exigir nova comprovação.

Validade exige token encontrado, sem revogação, `now < ExpiresAtUtc` e titular incompleto. Atividade não muda o vencimento. Nova prova válida emite nova autorização; cookie nunca renova a anterior. Índice por OwnerId para revogação, checks de intervalo de dez minutos desde VerifiedAtUtc e revogação não anterior à comprovação. Sem principal financeiro; token original somente no cookie restrito, não no banco/log/auditoria.

## OwnerSession existente

Preservar ID, revogação e trinta minutos de inatividade. Somente titular concluído pode ter sessão admitida/reconstruída. A conclusão revoga todas as sessões atomicamente, sem depender de SecurityStamp. Cookies de legado incompleto não acessam finanças desde a atualização.

## RegistrationAudit

Somente acréscimo: `Id`, `OwnerId` (FK restrita), `Action` (`Registered` ou `RegistrationCompleted`) e `OccurredAtUtc`. Gravar com a mutação, uma vez por sucesso. Sem perfil, e-mail, telefone, nascimento, senha, token ou corpo do pedido. Reenvio/conflito não gera outro evento de sucesso.

## Relações preservadas

- Owner tem zero ou mais contas financeiras; cada conta pertence a um único titular.
- Movimentação possui conta e titular coerentes pela FK composta existente.
- Obrigação pertence diretamente ao titular, sem conta obrigatória; baixa associa obrigação/movimentação do mesmo titular.
- Transição não altera registros, revisões financeiras, valores, origens ou vínculos.

## Estados e unidade de trabalho

| Estado | Operação | Resultado |
|---|---|---|
| Sem titular | Cadastro válido | Concluído, sem sessão, retorno ao login. |
| Sem titular | Cadastros simultâneos | Um vencedor no slot 1, outro recebe conflito orientativo. |
| Incompleto | Prova válida | Autorização restrita, sem acesso financeiro. |
| Incompleto | Conclusão válida dentro do prazo | Mesmo titular concluído, novo perfil/hash, sessões/autorizações revogadas. |
| Incompleto | Falha/expiração | Dados persistidos anteriores intactos; interface mantém somente dados não secretos. |
| Concluído | Nova prova/conclusão | Rejeição, sem reabrir cadastro ou sobrescrever credencial. |
| Concluído | Login por e-mail válido | Sessão financeira existente. |

Emissão e conclusão bloqueiam a mesma linha Owner e revalidam estado/versão. Na conclusão, verificar autorização e prazo novamente no ponto da mutação via TimeProvider. Perfil, credencial, data de conclusão, revogação em lote e auditoria compartilham transação/DbContext Identity. Conflito de ConcurrencyStamp ou falha provoca rollback integral; não sobrescrever vencedor.

## Migração

Incremental sobre a 003: acrescentar campos/tabelas/índices/checks sem recriar banco ou mudar IDs/FKs/hash legado. Zero titulares permite cadastro novo; um titular permanece incompleto, ajustando slot para 1 se necessário; mais de um aborta sem excluir dados. Sem titular padrão ou perfil fictício.

Validar SQL novo/atualizado, rollback, dois cadastros, duas conclusões e prova concorrente. Fixtures de acesso indevido devem usar IDs inexistentes/portas controladas ou testar explicitamente a rejeição de base inconsistente; não afrouxar singleton de produção para acomodar testes.
