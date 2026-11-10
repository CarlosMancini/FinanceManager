# 003 · Plano técnico

## Resumo

Registrar despesas previstas, acompanhar vencimentos e dar baixa conciliando com o realizado.

## Contexto técnico

Angular e TypeScript no frontend, ASP.NET Core e EF Core no backend, SQL Server na persistência. Consultar o plano da fundação para o arranjo da solução.

## Verificação da constituição

Atomicidade, idempotência, integridade das baixas e isolamento por titular. Aplicar os critérios globais da [constituição](../../.specify/memory/constitution.md).

## Estrutura do projeto

A documentação da feature fica nesta pasta. Código em "src/backend" e "src/frontend", com testes em "tests".

## Estratégia de implementação

Depende da feature 002. Persistir `Bill` com titular, descrição, valor, vencimento, estado de cancelamento e versão. Persistir `BillSettlement` com titular, obrigação, movimentação, indicação de criação na baixa e metadados UTC de criação/reversão. Calcular situação na leitura, usando a baixa ativa e o relógio civil. Usar chaves que preservem a titularidade nas referências e índices únicos filtrados para baixa ativa por obrigação e por movimentação.

Executar baixa e reversão em transações SQL. Na baixa, persistir registro de idempotência com unicidade por titular e chave, impressão digital do pedido e resultado original na mesma transação. Mesmo pedido retorna o resultado original, inclusive após reversão. Mesma chave com outro pedido retorna conflito. Para nova baixa após reversão, usar outra chave. Proteger operações concorrentes com restrições do banco e atualização condicionada pela versão da obrigação e da movimentação. A baixa e a reversão avançam as duas versões, inclusive quando os dados da movimentação seguem iguais. Operações de edição e cancelamento validam vínculo e versão de forma atômica.

No Angular, oferecer filtros, detalhe e ações de baixa e reversão. Mostrar o efeito da reversão antes de confirmar. Verificar datas antes, durante e depois do vencimento, valor divergente, replay, concorrência, falha entre gravações e reversão nas duas origens. Conferir os totais com os dados persistidos em SQL Server.
