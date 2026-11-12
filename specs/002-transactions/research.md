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

## Navegação condicionada por contas

**Decisão.** Reutilizar a consulta autenticada de contas em guards de rota. A entrada pós-login resolve primeiro a lista: zero contas abre o cadastro de conta; caso contrário, abre o histórico. A ação e a rota de nova movimentação verificam a existência de conta ativa, enquanto contas arquivadas continuam visíveis no histórico.

**Motivo.** Uma única fonte de decisão evita divergência entre botão, URL direta e redirecionamento após login, sem duplicar regras financeiras no cliente.

**Alternativas avaliadas.** Decidir apenas dentro de cada componente causaria transição visual para telas inválidas e repetiria consultas. Alterar a API de autenticação para devolver contas acoplaria sessão ao módulo financeiro.

## Entrada monetária em BRL

**Decisão.** Manter no formulário uma string visual `R$ X,XX` construída exclusivamente a partir de dígitos digitados ou colados e converter deterministicamente para a string decimal invariante antes do envio. Não converter o valor para `Number`. Bloquear envio com qualquer campo obrigatório ausente ou valor zero. A validação final de presença e valor positivo continua no servidor, inclusive para chamadas fora da interface.

**Motivo.** A abordagem preserva centavos e o contrato de precisão, permite edição previsível e mantém apresentação brasileira separada do transporte invariante.

**Alternativas avaliadas.** `input type="number"` não apresenta moeda brasileira de forma consistente. Ponto flutuante pode perder centavos. Uma biblioteca de máscara acrescentaria dependência para uma transformação pequena e determinística.

## Diálogos e cadastros consecutivos

**Decisão.** Usar diálogos modais acessíveis controlados pelo componente. O diálogo pós-cadastro só aparece depois do sucesso confirmado. **Sim** inicia outro rascunho com nova chave de idempotência; **Não** ou fechamento retorna ao histórico. Correção e cancelamento só chamam a API após confirmação explícita.

**Motivo.** O estado de tentativa permanece inequívoco e a confirmação reduz alterações acidentais sem mudar o contrato HTTP nem as invariantes do domínio.

**Alternativas avaliadas.** `window.confirm` oferece pouco controle de texto, acessibilidade e testes. Reutilizar a mesma chave após **Sim** transformaria um novo lançamento intencional em reenvio da tentativa anterior.

## Testes da interface

**Decisão.** Habilitar o builder de testes unitários já fornecido por `@angular/build` e fixar Vitest 4.0.8 como dependência de desenvolvimento. Cobrir guards, conversão monetária e decisões dos diálogos em testes de componente, mantendo a validação completa no navegador.

**Motivo.** As novas regras são predominantemente de apresentação e navegação; verificá-las apenas por testes do backend não detectaria regressões de botão, rota, foco ou envio prematuro.

**Alternativas avaliadas.** Manter somente validação manual reduziria a repetibilidade. Introduzir uma suíte ponta a ponta separada ampliaria ferramentas e manutenção além do necessário para estes componentes locais.
