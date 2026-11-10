# 002 · Plano técnico · Movimentações manuais

**Feature** `002-transactions` · **Data** 2026-11-10 · [Especificação](spec.md)

## Resumo

Registrar, corrigir e cancelar receitas e despesas manuais e consultar o histórico por conta, período e natureza. A criação usa uma chave por tentativa para que reenvios gerem uma única movimentação. A consulta devolve página e totais juntos, inclui cancelamentos no histórico e abre no mês civil atual em São Paulo. A navegação direciona titulares sem contas ao cadastro, impede novos lançamentos sem conta ativa, formata valores em BRL, permite cadastros consecutivos e confirma correções e cancelamentos.

## Contexto técnico

| Área | Decisão |
|---|---|
| Linguagem e dependências | SDK .NET 10.0.403, C# 14, ASP.NET Core e EF Core 10.0.14. Angular 22.2.5, TypeScript 6.0.3 e Node 24.22.2. Reutilizar as versões e lockfiles da [fundação](../001-financial-foundation/research.md#stack-e-testes). |
| Persistência | SQL Server 2025 Express LocalDB no Windows, via EF Core. Acrescentar migração versionada e validá-la em banco novo e em banco com a fundação aplicada. |
| Testes | xUnit v3 com Microsoft Testing Platform, relógio controlado, testes HTTP com `WebApplicationFactory` e integração em SQL Server separado. Acrescentar o builder `@angular/build:unit-test` com Vitest 4.0.8 fixado para guards, máscara monetária e componentes; validar também a interface Angular pelo fluxo local. |
| Plataforma e escopo | Aplicação web responsiva, um titular em loopback HTTPS, movimentações manuais em BRL. A feature 001 fornece sessão, contas e infraestrutura. |
| Navegação e formulários | Guards autenticados consultam as contas antes de liberar histórico e cadastro. O histórico abre após login quando existe ao menos uma conta; sem contas, a navegação abre o cadastro. O formulário monetário mantém uma representação textual em BRL separada da string decimal invariante enviada à API. Diálogos modais acessíveis confirmam continuação, correção e cancelamento. |
| Desempenho e diagnóstico | Paginar no banco com até 100 itens por resposta e agregar sobre o filtro completo em SQL. Verificar o plano de execução com histórico sintético e confirmar que a aplicação não materializa todos os lançamentos. Reutilizar o tratamento de falhas da fundação e registrar auditoria mínima UTC nas mutações. |

## Verificação da constituição

| Princípio | Aplicação ao desenho |
|---|---|
| I · Especificação | A matriz abaixo relaciona MOV-01 a MOV-07 aos testes. A feature 003 completa a proteção de baixas de MOV-03. |
| II · DDD, MVC e persistência | Angular apresenta os dados. Controllers delegam aos serviços de aplicação. O domínio valida os invariantes e a infraestrutura executa consultas e gravações SQL. |
| III · Integridade financeira | Valores decimais, data civil e relógio injetado, auditoria UTC, transações atômicas e revisão condicional. A chave de tentativa tem unicidade por titular. |
| IV · Segurança e privacidade | Sessão, antiforgery, HTTPS local e titularidade reaproveitados da fundação. Consultas e referências filtradas pelo titular, sem dados financeiros nos logs. |
| V · Integrações | Origem manual explícita. Importação e integrações bancárias ficam fora da feature. |
| VI · Testes | Casos de uso e regras no xUnit. Corridas, migração, restrições e valores em SQL Server. Verificação da interface e do contrato HTTP. |

Verificação concluída antes da pesquisa e repetida após o desenho. Os artefatos atendem à [constituição](../../.specify/memory/constitution.md) sem exceções.

## Estrutura do projeto

| Camada | Local e responsabilidade |
|---|---|
| Domínio | `src/backend/FinanceManager.Domain/Transactions/`, entidade, estados e regras monetárias e temporais. |
| Aplicação | `src/backend/FinanceManager.Application/Transactions/`, casos de uso, filtros, data padrão e portas. |
| Infraestrutura | `src/backend/FinanceManager.Infrastructure/Persistence/`, mapeamento, repositório, migração, concorrência e auditoria. |
| API | `src/backend/FinanceManager.Api/Controllers/` e `Http/`, contrato MVC, versões e erros em português. |
| Interface | `src/frontend/src/app/transactions/`, tela autenticada, formulário, filtros, formatação monetária, diálogos e navegação; `src/frontend/src/app/accounts/` recebe o redirecionamento de onboarding. |
| Testes | `tests/FinanceManager.UnitTests/Transactions/` e `tests/FinanceManager.IntegrationTests/Transactions/`, seguindo as convenções da fundação. |

## Estratégia de implementação

1. Criar `FinancialTransaction`, configurações de persistência e migração conforme o [modelo de dados](data-model.md).
2. Implementar registro idempotente, correção e cancelamento com auditoria atômica. Reutilizar a convenção de ETag da fundação. Uma correção pode manter a conta arquivada original e só pode trocar para outra conta ativa do titular.
3. Implementar consulta por titular, conta, natureza e datas inclusivas, ordenada por data e ID decrescentes. Aplicar os filtros uma vez e devolver página, contagem e totais em uma resposta consistente.
4. Integrar os fluxos no Angular e executar a [validação local](quickstart.md). A feature 003 deverá testar vínculo ativo e versão na mesma operação e avançar a versão da movimentação em cada baixa ou reversão. Essa proteção de MOV-03 depende do vínculo entregue pela 003.
5. Centralizar a decisão de navegação em guards que reutilizam `AccountsApiService`: a rota inicial consulta contas; zero contas redireciona ao cadastro, enquanto o histórico permanece inicial para titulares com contas. A rota de nova movimentação exige uma conta ativa e a ação do histórico reflete a mesma condição.
6. Manter o valor digitado como texto monetário. A apresentação aceita dígitos e produz `R$ X,XX`; o cliente remove símbolos e separadores visuais para enviar a string decimal invariante já aceita pelo contrato, sem usar ponto flutuante.
7. Após `POST` confirmado, abrir um diálogo de continuação. **Sim** descarta o estado concluído, cria nova chave de idempotência e restaura os padrões do formulário; **Não** ou fechamento navega ao histórico. Antes de `PUT` de correção ou cancelamento, abrir confirmação e só enviar após aceite. Os diálogos preservam foco, permitem teclado e devolvem foco ao acionador quando não prosseguem.

## Matriz de verificação

| Critério | Verificação |
|---|---|
| MOV-01 | xUnit para valor, descrição, natureza, data e conta. SQL e HTTP para reenvio após resposta perdida, reenvios concorrentes, chave com conteúdo divergente, dados iguais com nova chave, conta arquivada durante gravação e rollback da auditoria. |
| MOV-02 | xUnit para limites civis, virada do mês em São Paulo e validação de filtros. SQL e HTTP para ordenação em empate, paginação, titularidade e histórico de contas arquivadas. Navegador para mês inicial, estado vazio e status **Cancelada**. |
| MOV-03 | xUnit para estados e troca de conta. SQL e HTTP para auditoria, edição ou cancelamento concorrente e versão desatualizada. Cenários com baixa vinculada ficam na feature 003. |
| MOV-04 | SQL e HTTP para totais com filtros e páginas, zero quando vazio ou só cancelado, líquido negativo, centavos e agregados acima do limite individual. Exercitar leitura concorrente e conferir lista e totais no navegador. |
| MOV-05 | Testes de navegação para login com zero contas e com conta existente, estado desabilitado sem conta ativa e acesso direto ao formulário sem conta. Navegador para foco no cadastro e ausência de caminho que envie lançamento sem conta ativa. |
| MOV-06 | Testes da transformação textual entre BRL e decimal invariante, incluindo zero, centavos, colagem, remoção de dígitos e limite máximo. Navegador para **Sim**, **Não** e fechamento do diálogo, verificando nova chave por tentativa e dois cadastros consecutivos. |
| MOV-07 | Testes de componente para impedir chamadas antes da confirmação e preservar estado ao voltar ou fechar. Navegador e API para correção confirmada, cancelamento confirmado, versão obsoleta e cancelamento definitivo. |

## Artefatos de desenho

- [Pesquisa e decisões](research.md)
- [Modelo de dados](data-model.md)
- [Contrato HTTP](contracts/api.md)
- [Guia de validação](quickstart.md)
