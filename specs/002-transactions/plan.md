# 002 · Plano técnico

## Resumo

Registrar receitas e despesas realizadas e consultar o histórico por conta e período.

## Contexto técnico

Angular e TypeScript no frontend, ASP.NET Core e EF Core no backend, SQL Server na persistência. Consultar o plano da fundação para o arranjo da solução.

## Verificação da constituição

BRL decimal, calendário de São Paulo, isolamento por titular e controle de concorrência. Aplicar os critérios globais da [constituição](../../.specify/memory/constitution.md).

## Estrutura do projeto

A documentação da feature fica nesta pasta. Código em "src/backend" e "src/frontend", com testes em "tests".

## Estratégia de implementação

Depende da feature 001. Criar `FinancialTransaction` com titular, conta, natureza, valor, data civil, descrição, origem manual, estado, metadados de auditoria e versão para concorrência. Descrição obrigatória de até 200 caracteres. Usar `DateOnly` no domínio e `date` no SQL Server. Utilizar `decimal(19,2)` e representar dinheiro como string no contrato HTTP. Validar conta, titular, data e valor no servidor. Uma correção pode manter a conta original arquivada, mas só pode transferir o lançamento para outra conta ativa do titular.

Criar operações REST para cadastrar, listar, alterar e cancelar. Ordenar por data civil e ID decrescentes e paginar. Aplicar os mesmos filtros à consulta de totais, excluindo cancelamentos. Usar versão esperada em alterações e cancelamentos e retornar conflito para uma versão desatualizada. A feature 003 acrescentará a proteção contra alterações quando houver baixa ativa.

Testar limites monetários e de data, cancelamento, paginação com totais, datas limítrofes em São Paulo e isolamento por titular. Conferir filtros e precisão com SQL Server.
