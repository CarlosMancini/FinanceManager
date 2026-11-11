# Pesquisa · 003 · Contas a pagar e baixas

## Limites e dependências

**Decisão.** Reutilizar a feature 002 concluída, incluindo sua validação T033, o modelo e a migração de movimentações, a API, a interface, os testes, os serviços e as portas existentes. Reutilizar também as versões fixadas na fundação, a sessão da API e a proteção CSRF.

**Motivo.** A 002 é a fonte do realizado. O vínculo usa o mesmo registro de despesa, e suas operações de edição e cancelamento precisam reconhecer baixas ativas.

**Alternativas avaliadas.** Um segundo registro para representar o realizado duplicaria regras e poderia contar uma saída duas vezes. Uma integração bancária não faz parte deste escopo.

## Estados, consulta e escolha de despesa

**Decisão.** Derivar a situação a partir do cancelamento, da baixa ativa e do vencimento comparado ao dia civil de São Paulo. Consultar todas as obrigações em aberto por padrão e aplicar o período apenas quando informado. Ordenar por vencimento e ID. Calcular página, contagem e soma no mesmo filtro e numa transação curta `SERIALIZABLE`, incluindo apenas obrigações em aberto na soma. Usar a precisão do agregado SQL e serializá-lo em formato decimal invariante. Oferecer uma lista paginada de despesas elegíveis para a obrigação, incluindo as de contas arquivadas.

**Motivo.** Situações temporais mudam sem uma gravação. Uma leitura consistente evita exibir a lista de antes da baixa com a soma de depois. O seletor pode encontrar despesas antigas sem adotar o mês padrão do histórico da 002.

**Alternativas avaliadas.** Persistir a situação temporal exigiria atualização à meia-noite. Chamadas HTTP separadas para lista e soma admitiriam respostas de momentos distintos. Isolamento por snapshot também atenderia à leitura, mas exigiria habilitar uma opção adicional no banco. Reutilizar o histórico sem uma projeção de elegibilidade incluiria itens cancelados, já vinculados ou com valor diferente. [Isolamento e bloqueios no SQL Server](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide).

## Baixa, reversão e concorrência

**Decisão.** Registrar baixa, despesa nova quando aplicável, recibo idempotente e auditoria no mesmo commit. Usar chaves compostas com titularidade, índices únicos filtrados por baixa ativa e atualizações condicionadas por `rowversion`. Avançar as revisões da obrigação e da movimentação na baixa e na reversão. Para criar uma despesa, manter a leitura da conta ativa protegida até o commit, seguindo a estratégia de bloqueio da 002. Reverter pelo ID da baixa, preservando o vínculo histórico e cancelando só a despesa criada por ele.

**Motivo.** A unicidade impede baixas simultâneas do mesmo registro. As atualizações condicionais também impedem que uma edição concorrente passe por cima da validação. O ID da baixa permite repetir a reversão sem atingir um vínculo mais novo.

**Alternativas avaliadas.** Uma validação prévia na aplicação sem restrições SQL deixaria uma janela de concorrência. Um vínculo sem histórico não permitiria rastrear a origem da reversão. [Concorrência no EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [transações no EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions) e [índices filtrados no SQL Server](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-filtered-indexes).

## Reenvio e origem

**Decisão.** Persistir chave por titular, impressão digital da solicitação normalizada e recibo imutável. A impressão digital inclui os tokens de versão enviados no pedido original, não as revisões correntes relidas do banco. Consultar o recibo antes das regras dependentes de estado e reler após rollback de uma corrida. Retornar conflito para mesma chave com entrada diferente. A despesa gerada tem origem `BillSettlement` e usa o recibo da baixa para deduplicação. A despesa vinculada preserva sua origem.

**Motivo.** Uma resposta perdida pode ser recuperada mesmo após a reversão. A origem e a auditoria distinguem criação manual, vínculo e criação pela baixa. A SPA consulta novamente o detalhe para mostrar o estado atual.

**Alternativas avaliadas.** Recalcular o retorno a partir do estado atual alteraria o resultado de um reenvio. Gravar a despesa gerada como cadastro manual exigiria uma segunda chave de tentativa e esconderia a origem no histórico.
