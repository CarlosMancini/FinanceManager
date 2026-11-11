# 003 · Plano técnico · Contas a pagar e baixas

**Feature** `003-bills-and-payments` · **Data** 2026-11-10 · [Especificação](spec.md)

## Resumo

Acompanhar obrigações em aberto e seus vencimentos, baixar pelo valor integral e reverter com rastreabilidade. A lista inicial inclui obrigações em aberto de qualquer mês, e o total considera os filtros de situação e período. A baixa pode aproveitar uma despesa ativa de conta arquivada ou criar uma despesa em conta ativa copiando o valor e a descrição da obrigação.

## Contexto técnico

| Área | Decisão |
|---|---|
| Linguagem e dependências | SDK .NET 10.0.403, C# 14, ASP.NET Core e EF Core 10.0.14. Angular 22.2.5, TypeScript 6.0.3 e Node 24.22.2. Reutilizar versões fixadas e lockfiles. |
| Persistência | SQL Server via EF Core. Usar SQL Server 2025 Express LocalDB na validação local e a instância SQL Server do CI na integração. Versionar a migração sobre a 002. |
| Testes | xUnit v3 4.0.3 com Microsoft Testing Platform, relógio controlado, `WebApplicationFactory`, bases SQL sintéticas separadas e validação no navegador. |
| Plataforma e escopo | Monólito modular web responsivo, em loopback HTTPS para um titular. Operações manuais em BRL. |
| Desempenho | Paginar no SQL com até 100 registros por resposta e agregar no banco sobre o filtro inteiro. Verificar com volume sintético que a consulta não carrega todo o histórico na aplicação. |
| Dependências internas | A fundação fornece sessão e contas. A feature 002, incluindo sua validação T033, está concluída e fornece o modelo de movimentações, migração, API, interface e testes. |

## Verificação da constituição

| Princípio | Aplicação |
|---|---|
| I · Especificação | A matriz liga PAG-01 a PAG-05 e MOV-03 a verificações observáveis. |
| II · DDD, MVC e persistência | Angular apresenta os dados. Controllers delegam aos serviços de aplicação. O domínio aplica invariantes, e a infraestrutura implementa consultas, transações e migrações. |
| III · Integridade | BRL decimal, calendário civil injetável, auditoria UTC, baixa e reversão atômicas e recibo idempotente. A saída realizada permanece em uma única movimentação. |
| IV · Segurança e privacidade | Reutilizar sessão e CSRF da fundação. Escopar dados por titular, usar HTTPS local e manter dados financeiros fora dos logs. |
| V · Integrações | Movimentações preservam origem manual ou de baixa. A feature usa somente fontes internas. |
| VI · Testes | xUnit cobre regras e casos de uso. SQL Server verifica migração, precisão, concorrência e rollback. A validação também passa pela API e pela interface. |

A avaliação antes da pesquisa e a revisão após o desenho atendem à [constituição](../../.specify/memory/constitution.md) sem exceções.

## Estrutura do projeto

Os artefatos da feature ficam ao lado da especificação. O código segue as camadas existentes.

| Camada | Local e responsabilidade |
|---|---|
| Domínio | `src/backend/FinanceManager.Domain/Bills/` e `Transactions/`, regras da obrigação, baixa e movimentação. |
| Aplicação | `src/backend/FinanceManager.Application/Bills/` e `Transactions/`, casos de uso, leitura e portas de persistência. |
| Infraestrutura | `src/backend/FinanceManager.Infrastructure/Persistence/`, consultas, mapeamento, transações e migrações. |
| API | `src/backend/FinanceManager.Api/Controllers/` e `Http/`, HTTP, sessão, validação de entrada e erros em português. |
| Interface | `src/frontend/src/app/bills/` e `transactions/`, lista, filtros, formulários e estado de baixa. |
| Testes | `tests/FinanceManager.UnitTests/` e `tests/FinanceManager.IntegrationTests/`, com casos de contas a pagar e regressões de movimentações. |

## Estratégia de implementação

1. Modelar obrigação, vínculo e recibo conforme o [modelo de dados](data-model.md). Acrescentar a migração com referências por titular, índices de baixa ativa e origem de movimentação gerada, preservando registros da 002.
2. Implementar cadastro, edição, cancelamento e consulta. Calcular as situações com a mesma data civil em toda a operação. Devolver página, contagem e total coerentes com o filtro.
3. Executar a baixa em uma transação com a despesa nova, quando aplicável, a auditoria e o recibo. A reversão opera pelo ID da baixa e cancela somente a despesa que ela criou. Usar versões e restrições para proteger a concorrência com as mutações de obrigações e de movimentações.
4. Expor o [contrato HTTP](contracts/api.md). Na interface, manter o total associado aos filtros, apresentar estados de carregamento, vazio e erro, e permitir selecionar despesas elegíveis de contas arquivadas. Ao criar despesa, mostrar o valor e a descrição copiados e oferecer apenas contas ativas. Antes de reverter, informar o efeito sobre a despesa.
5. Aplicar e validar as migrações e executar o [roteiro local](quickstart.md).

## Matriz de verificação

| Critério | Verificação |
|---|---|
| PAG-01 | xUnit para descrição, valor, cancelamento e estado. HTTP e navegador para cadastro, edição, histórico e rejeição de revisão antiga. |
| PAG-02 | xUnit para dia civil, virada do dia e filtros. SQL e HTTP para vencidas de meses anteriores, limites inclusivos, paginação, precisão do total, resultado vazio e leitura durante baixa concorrente. |
| PAG-03 | xUnit para valor idêntico, conta, data, origem e descrição copiada. SQL e HTTP para vínculo em conta arquivada, criação em conta ativa, recibo após resposta perdida e reversão, chave divergente e concorrência. |
| PAG-04 | xUnit, SQL e interface para ambas as origens da despesa, totais e tentativa repetida de reversão após nova baixa. |
| PAG-05 e MOV-03 | xUnit para bloqueio por baixa ativa e para casos de uso de conflito. SQL e HTTP para duas baixas da mesma obrigação ou movimentação, edição e cancelamento concorrentes, arquivamento durante criação, titularidade, CSRF, rollback e ausência de auditoria duplicada. |

## Artefatos de desenho

- [Pesquisa e decisões](research.md)
- [Modelo de dados](data-model.md)
- [Contrato HTTP](contracts/api.md)
- [Guia de validação](quickstart.md)
