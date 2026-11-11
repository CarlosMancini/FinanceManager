# Validação local · 003 · Contas a pagar e baixas

Execute após implementar as features 002 e 003. Use os pré-requisitos e os comandos de [preparação da fundação](../001-financial-foundation/quickstart.md#preparar-e-iniciar) para configurar conexões separadas, restaurar as dependências fixadas, aplicar migrações, provisionar o titular, compilar a SPA e iniciar a API em HTTPS.

Valide a migração tanto em banco sintético novo quanto em banco sintético na versão 002 com movimentações manuais existentes. Na atualização, confirme que IDs, origem, histórico e reenvios de criação manual continuam funcionando. Use o [contrato HTTP](contracts/api.md) para preparar requisições de teste.

## Testes automatizados

Na raiz, após a restauração e com `ConnectionStrings__FinanceManagerTests` apontando para o SQL Server de teste:

```powershell
dotnet test --project .\tests\FinanceManager.UnitTests\FinanceManager.UnitTests.csproj --no-restore
dotnet test --project .\tests\FinanceManager.IntegrationTests\FinanceManager.IntegrationTests.csproj --no-restore
npm --prefix .\src\frontend test
```

Confira a [matriz de verificação](plan.md#matriz-de-verificação). Use relógio controlado para os limites civis, sincronize as requisições concorrentes no teste e injete uma falha entre gravações da baixa. Verifique no SQL que há no máximo um vínculo ativo por obrigação e por despesa, que versões antigas não permitem mutação, e que o rollback deixa despesa, vínculo, recibo e auditoria no estado anterior.

## Navegador e API

1. Entre em `https://localhost:7043`. Crie três obrigações sintéticas de R$ 40,00, R$ 30,00 e R$ 20,00 com vencimento, respectivamente, no mês anterior, hoje e amanhã no calendário de São Paulo. Confira os três estados na lista inicial e o total de R$ 90,00. Filtre por vencidas e inclua a primeira data no limite do período. Espere R$ 40,00. Pela API, use `pageSize=1` na lista em aberto e confira o mesmo total de `"90.00"` nas três páginas.
2. Registre uma despesa manual de R$ 40,00 e arquive a conta. Vincule essa despesa à obrigação vencida. Confira a saída original no histórico e o total em aberto de R$ 50,00 na consulta inicial. Tente editar ou cancelar a movimentação com a revisão atual e espere `409 TRANSACTION_LINKED`. Reverta a baixa e confirme que a despesa original permanece ativa e que o total volta a R$ 90,00.
3. Dê baixa na obrigação de R$ 30,00 criando a despesa. Tente primeiro uma conta arquivada e depois uma data futura para verificar as rejeições. Em conta ativa e com data válida, confira que valor e descrição foram copiados e que a origem é `billSettlement`. O total passa a R$ 60,00.
4. Repita a solicitação bem-sucedida da etapa anterior com a mesma chave e corpo. Confira o mesmo recibo e uma única despesa gerada. Reverta a baixa e confirme a despesa cancelada, a retirada do valor do realizado e o retorno da obrigação ao total em aberto. Repita o pedido original e espere o mesmo recibo. Mude um campo válido mantendo a chave e espere `409 IDEMPOTENCY_KEY_CONFLICT`, sem novo recibo ou despesa e sem alteração do estado da baixa.
5. Faça nova baixa com nova chave e a versão atual. Repita a reversão da baixa antiga pelo ID original e confira que a nova segue ativa. Cadastre e cancele outra obrigação. Consulte separadamente pagas e canceladas e confira total em aberto de `"0.00"`. Verifique `401` sem sessão, `403` sem CSRF em mutação autenticada e `404` para IDs de outro titular nas bases de teste.
