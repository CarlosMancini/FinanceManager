# Decisões técnicas · 002 · Movimentações manuais

## Stack e separação de responsabilidades

**Decisão.** Reutilizar a [stack fixada da fundação](../001-financial-foundation/research.md#stack-e-testes), o relógio, a sessão, o antiforgery, os erros HTTP e a infraestrutura de testes. Organizar as regras em domínio, os casos de uso e portas em aplicação, o SQL Server em infraestrutura e a entrada HTTP em controllers MVC.

**Motivo.** A fundação já executa essas responsabilidades e a feature acrescenta um módulo ao mesmo aplicativo local.

**Alternativas avaliadas.** Serviço separado, biblioteca monetária externa e novo mecanismo de autenticação adicionariam dependências e pontos de falha para o mesmo titular.

## Reenvio de cadastro

**Decisão.** Gerar um UUID por tentativa no formulário e reutilizar a chave e o pedido original enquanto houver dúvida sobre o resultado. Persistir na movimentação a chave e a impressão digital SHA-256 da entrada normalizada, com índice único por titular e chave. Gravar criação e auditoria na mesma transação. Após conflito desse índice, reler com contexto limpo e comparar a impressão digital. Reenvio equivalente devolve a mesma identidade e a representação atual. Mesma chave com outros dados recebe conflito.

**Motivo.** O índice arbitra a corrida mesmo após reinício. Os metadados imutáveis permitem reconhecer o pedido mesmo depois de edição, cancelamento ou arquivamento da conta.

**Alternativas avaliadas.** Comparar campos atuais confundiria lançamentos iguais intencionais e falharia após uma edição. Cache em memória perderia a garantia após reinício. Um recibo imutável com o corpo original duplicaria dados para uma garantia que MOV-01 limita à identidade da movimentação.

[Resiliência de conexão no EF Core](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency)

## Correção, cancelamento e conta ativa

**Decisão.** Reutilizar `rowversion` com ETag forte e `If-Match` para escrita condicional. Manter leitura do estado da conta de destino e escrita na mesma transação curta `REPEATABLE READ` ao criar ou trocar de conta. Encerrar a transação antes de reler após violação de unicidade. Usar a mesma transação da alteração para sua auditoria.

**Motivo.** A revisão da movimentação detecta alterações concorrentes no lançamento. O isolamento mantém a leitura da conta estável até o commit, ordenando gravação e arquivamento concorrentes. Se a conta for arquivada primeiro, o cadastro ou a troca deve ser rejeitado.

**Alternativas avaliadas.** Validação fora da transação deixa uma janela entre a leitura da conta e a gravação. Uma trava em memória não protege outros processos.

[Concorrência no EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) · [Isolamento no SQL Server](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide?view=sql-server-ver17)

## Calendário, paginação e totais

**Decisão.** Converter o instante do relógio uma vez por consulta para America/Sao_Paulo e derivar as datas civis do primeiro e último dia do mês. A API aplica os mesmos filtros à lista e aos totais. Executar contagem, totais e página em uma transação curta de leitura `SERIALIZABLE`. Usar paginação por deslocamento com ordem única por data e ID decrescentes.

**Motivo.** A resposta mantém uma visão consistente mesmo durante correções concorrentes. A paginação atende a navegação por número de página. O índice por titular, data e ID apoia a consulta inicial.

**Alternativas avaliadas.** Páginas por cursor seriam adequadas para navegação sequencial em volumes maiores. Consultas independentes em `READ COMMITTED` podem observar estados distintos. Isolamento `SNAPSHOT` exigiria habilitar e operar uma opção de banco adicional.

[Paginação no EF Core](https://learn.microsoft.com/en-us/ef/core/querying/pagination) · [Isolamento no SQL Server](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide?view=sql-server-ver17)

## Precisão e diagnóstico

**Decisão.** Persistir valores individuais em `decimal(19,2)`. Agregar no SQL Server com `SUM` em `decimal(38,2)`, calculando também o líquido pela soma dos valores com o sinal de cada natureza. Projetar os três resultados como strings invariantes de duas casas, sem converter o agregado para `decimal` do CLR. Na interface, formatar as strings em BRL sem convertê-las em `Number`. Usar o padrão de diagnóstico e auditoria da fundação com identificadores e metadados mínimos.

**Motivo.** Totais podem ultrapassar o limite de um lançamento. O cálculo do líquido por soma assinada mantém a escala dos centavos. Páginas limitadas e agregação no banco evitam transportar o conjunto completo.

**Alternativas avaliadas.** Somar só a página mudaria os resultados. Ponto flutuante pode perder centavos. Uma tabela de totais pré-calculados adicionaria sincronização a cada escrita e a cada filtro.

[Tipos de retorno de SUM no SQL Server](https://learn.microsoft.com/en-us/sql/t-sql/functions/sum-transact-sql?view=sql-server-ver17)
