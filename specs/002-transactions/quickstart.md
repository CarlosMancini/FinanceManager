# Validação local · 002 · Movimentações manuais

Execute após implementar a feature. Use as versões fixadas e as bases sintéticas separadas descritas no [guia da fundação](../001-financial-foundation/quickstart.md). Siga o preparo desse guia para configurar o SQL Server, restaurar em modo bloqueado, aplicar as migrações, provisionar o titular se a base for nova, compilar a SPA e iniciar a API em HTTPS. Verifique a migração também em uma base sintética com a fundação aplicada.

## Executar testes

Na raiz do repositório, após a restauração e com a conexão de testes configurada:

```powershell
dotnet test --project .\tests\FinanceManager.UnitTests\FinanceManager.UnitTests.csproj --no-restore
dotnet test --project .\tests\FinanceManager.IntegrationTests\FinanceManager.IntegrationTests.csproj --no-restore
```

Confira a [matriz do plano](plan.md#matriz-de-verificação). A suíte deve verificar virada do mês com relógio controlado, paginação com datas iguais, rejeições de titularidade, concorrência de criação e arquivamento, falha antes do commit, revisão desatualizada e totais acima da precisão individual. Use a base SQL de teste para essas verificações.

## Conferir no navegador e na API

1. Entre em `https://localhost:7043` e crie duas contas ativas. Abra **Movimentações** e confira o primeiro e o último dia do mês civil atual em São Paulo e os totais zerados em uma base sem movimentações.
2. Na mesma conta e em uma data do mês até hoje, registre uma receita de R$ 100,00 e uma despesa de R$ 30,00. Em outra conta, registre R$ 50,00 de receita. Consulte a primeira conta pela API com `pageSize=1` e os limites inclusivos do mês. Cada página deve mostrar um item, com a mesma contagem de 2 e totais de `100.00`, `30.00` e `70.00`. Filtre por despesa e confira o líquido `-30.00`.
3. Corrija a despesa para R$ 25,00. Cancele-a e confira que o histórico ainda a mostra como **Cancelada**, com despesa total de `0.00` e líquido de `100.00` no filtro da primeira conta. Tente editar a movimentação cancelada e confira a rejeição.
4. Arquive a primeira conta. Consulte seu histórico e tente registrar uma nova despesa nela. Os registros anteriores devem continuar visíveis e o cadastro deve ser rejeitado. Edite uma movimentação ativa mantendo a conta original e depois troque para outra conta ativa.
5. Com sessão e CSRF válidos, envie um cadastro pelo [contrato HTTP](contracts/api.md). Repita a mesma chave e o mesmo corpo após descartar a primeira resposta. Confira `201` na primeira chamada, `200` no reenvio e o mesmo ID. Repita após editar ou cancelar e confira a representação atual com o mesmo ID. Mude o corpo com a mesma chave e espere `409`. Envie o corpo original com uma chave nova e confira a criação de outro ID.
6. Abra uma movimentação ativa em duas abas, salve uma alteração na primeira e tente salvar na segunda. Confira `412` e a orientação para recarregar. Pela API, confira `401` sem sessão e `403` ao omitir CSRF em uma mutação autenticada.
