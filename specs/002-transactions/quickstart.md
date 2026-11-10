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

1. Entre em `https://localhost:7043` com um titular sem contas. Confirme que o login abre o cadastro de conta com o formulário pronto para uso. Tente abrir `/transactions/new` diretamente e confirme o mesmo redirecionamento. No histórico, quando alcançável sem conta ativa, **Nova movimentação** deve estar desabilitada.
2. Cadastre duas contas ativas. Confirme que novos logins abrem **Movimentações**, com o primeiro e o último dia do mês civil atual em São Paulo e totais zerados. Arquive ambas e confirme que o histórico continua acessível, mas **Nova movimentação** fica desabilitada.
3. Reative uma conta e abra **Nova movimentação**. Digite `1`, `1234` e valores com centavos, conferindo a apresentação automática como `R$ 0,01`, `R$ 12,34` e equivalentes. Cole entrada inválida e confirme que apenas um valor BRL válido pode ser enviado.
4. Registre uma receita de R$ 100,00. No diálogo **“Adicionar mais uma movimentação?”**, escolha **Sim** e confirme formulário limpo, padrões restaurados e nova tentativa. Registre uma despesa de R$ 30,00 e escolha **Não**; confirme retorno ao histórico e dois IDs distintos. Repita fechando o diálogo e confira comportamento equivalente a **Não**.
5. Em outra conta, registre R$ 50,00 de receita. Consulte a primeira conta pela API com `pageSize=1` e os limites inclusivos do mês. Cada página deve mostrar um item, com a mesma contagem de 2 e totais de `100.00`, `30.00` e `70.00`. Filtre por despesa e confira o líquido `-30.00`.
6. Abra a edição da despesa, altere para R$ 25,00 e escolha **Voltar** na confirmação; nenhuma requisição deve ser enviada e o formulário deve permanecer preenchido. Confirme a correção em nova tentativa. Faça o mesmo com o cancelamento, primeiro voltando e depois confirmando. O histórico deve mostrar **Cancelada**, despesa total `0.00` e líquido `100.00`.
7. Arquive a primeira conta. Consulte seu histórico e tente registrar uma nova despesa nela. Os registros anteriores devem continuar visíveis e o cadastro deve ser rejeitado. Edite uma movimentação ativa mantendo a conta original e depois troque para outra conta ativa.
8. Com sessão e CSRF válidos, envie um cadastro pelo [contrato HTTP](contracts/api.md). Repita a mesma chave e o mesmo corpo após descartar a primeira resposta. Confira `201` na primeira chamada, `200` no reenvio e o mesmo ID. Repita após editar ou cancelar e confira a representação atual com o mesmo ID. Mude o corpo com a mesma chave e espere `409`. Envie o corpo original com uma chave nova e confira a criação de outro ID.
9. Abra uma movimentação ativa em duas abas, confirme uma alteração na primeira e tente confirmar a segunda com a versão anterior. Confira `412` e a orientação para recarregar. Pela API, confira `401` sem sessão e `403` ao omitir CSRF em uma mutação autenticada.
