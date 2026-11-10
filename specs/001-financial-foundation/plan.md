# Plano de implementação · 001 · Fundação financeira

**Feature** `001-financial-foundation` · **Data** 2026-11-09 · [Especificação](spec.md)

## Resumo

Entregar acesso local para um titular provisionado e cadastro, edição, arquivamento e reativação de contas financeiras. A aplicação Angular e a API ASP.NET Core compartilham a origem HTTPS. O SQL Server mantém as sessões revogáveis, as contas e as restrições de unicidade e concorrência.

## Contexto técnico

| Item | Decisão |
|---|---|
| Linguagens e versões | C# 14 em .NET 10 e TypeScript com Angular. [Matriz de versões e fixação](research.md#stack-e-testes). |
| Dependências | Angular CLI, ASP.NET Core MVC e Identity, antiforgery, EF Core e provedor SQL Server. |
| Armazenamento | SQL Server 2025 Express LocalDB. Migrações EF Core versionadas. Chaves do Data Protection no perfil do usuário local, protegidas pelo sistema operacional. |
| Testes | xUnit para regras e casos de uso isolados e integração HTTP/SQL Server. Verificação de ponta a ponta em navegador. |
| Plataforma | Windows no ambiente inicial. API restrita a loopback em HTTPS. Frontend responsivo em português. |
| Desempenho e escala | Um titular, carga interativa local. Consultas por titular e índice único para cadastro. A especificação não estabelece SLA numérico. |
| Restrições | BRL, sem saldo inicial, sem exclusão física da conta, sem cadastro público e sem integração bancária. |

## Verificação da constituição

| Princípio | Gate antes da pesquisa | Verificação após o desenho |
|---|---|---|
| I · Especificação | Rastrear o desenho a FND-01–04. | Contrato e matriz de testes referenciam todos os critérios. |
| II · Arquitetura | Monólito modular, MVC e DDD com controllers delegando casos de uso. | Domínio isolado de HTTP e EF Core. Aplicação usa portas, infraestrutura implementa persistência e identidade. Versões fixadas em [research.md](research.md). |
| III · Integridade | Preservar propriedade, estado e moeda em todas as operações. | Conta em BRL sem saldo, alterações protegidas por concorrência e unicidade no SQL Server, trilha de auditoria mínima em UTC. Movimentação e baixa pertencem às features seguintes. |
| IV · Segurança | Autenticar e validar titularidade no servidor. | Cookie seguro, sessão revogável, antiforgery, HTTPS loopback, provisionamento local e logs sem credenciais ou dados financeiros. |
| V · Integrações | Preservar fronteiras do domínio. | Escopo de entrada manual, com persistência e identidade atrás de portas. |
| VI · Testes | Exigir teste unitário para cada regra de negócio e integração para garantias reais da infraestrutura. | A matriz abaixo especifica resultados, rejeições e limites. O [quickstart](quickstart.md) cobre a validação HTTP, SQL Server e navegador. |

Nomes técnicos em inglês e mensagens de erro ao usuário em português. O gate de desenho foi aprovado sem exceções. A execução da matriz e a revisão de conformidade integram o critério de conclusão da implementação.

## Estrutura do projeto

### Documentação da feature

```text
specs/001-financial-foundation/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/
│   └── api.md
├── quickstart.md
└── tasks.md
```

### Código previsto na raiz do repositório

```text
FinanceManager.slnx
src/
├── backend/
│   ├── FinanceManager.Api/
│   ├── FinanceManager.Application/
│   ├── FinanceManager.Domain/
│   └── FinanceManager.Infrastructure/
└── frontend/
tests/
├── FinanceManager.UnitTests/
└── FinanceManager.IntegrationTests/
```

O backend roda em um processo. Controllers MVC adaptam HTTP e chamam serviços de aplicação. As entidades do domínio aplicam invariantes. A infraestrutura mantém EF Core, migrações, Identity e a implementação das portas. A API hospeda o build Angular para validação e execução local. Configurar a saída Angular em src/frontend/dist/finance-manager/browser e passar esse caminho à API em Angular:BrowserRoot. Em desenvolvimento, o proxy local pode manter a mesma origem visível pelo navegador.

## Estratégia de implementação

1. Fixar o ambiente e criar a solução, a SPA, os projetos de testes e as migrações iniciais. Preparar o comando local de provisionamento com entrada de senha protegida.
2. Implementar autenticação, antiforgery, isolamento por titular e relógio injetável. Centralizar a admissão de requisições autenticadas para aplicar os 30 minutos de inatividade e revogar a sessão no logout.
3. Implementar as operações de conta com chave de nome normalizada, índice único por titular, arquivamento reversível e versão HTTP respaldada por `rowversion`. Gravar alteração e auditoria mínima na mesma transação.
4. Construir telas de login e contas, com estados de carregamento, lista vazia, sessão expirada, conflito e falha de gravação. Em conflito de edição, manter o erro visível e exigir recarga antes de novo salvamento.
5. Executar os testes unitários, validar migrações e corridas reais em SQL Server e verificar o fluxo completo no navegador.

## Matriz de verificação

| Critério | Testes unitários xUnit | Verificação integrada |
|---|---|---|
| FND-01 · Titular e sessão | Provisionamento único, credenciais aceitas e rejeitadas, sessão ausente/revogada/expirada, 29 min 59 s e limite exato de 30 min, renovação por atividade admitida, rejeição sem renovação, revogação e isolamento por titular. | HTTPS, cookies, CSRF inclusive no login/logout, replay após logout, isolamento com titular sintético, requisições concorrentes sem regressão do relógio e falha de renovação quando a sessão vence. |
| FND-02 · Cadastro e edição | Tipos permitidos, BRL sem saldo inicial, normalização, duplicata ativa ou arquivada em cadastro e renomeação, mesmo nome da própria conta, mesmo nome de outro titular, versão correta e versão desatualizada sem alteração. | Migrações, persistência após reinício, inserções e renomeações concorrentes, edição e arquivamento concorrentes com `If-Match`, resposta de conflito e auditoria atômica. |
| FND-03 · Arquivamento | Transições ativa/arquivada, preservação de identidade e histórico, disponibilidade para movimentação conforme estado e proteção contra revisão antiga. | Arquivar e reativar por API e navegador, manter conta consultável e verificar o estado persistido. A feature 002 valida o consumo da regra no registro de movimentações. |
| FND-04 · Validação e falhas | Nome em branco, limites de 120 e 121 unidades UTF-16 após trim, tipo inválido e falha de persistência propagada sem sucesso. | Erros em português, estados de lista vazia/sessão expirada, formulário em conflito e falha de gravação sem falsa confirmação. |

## Controle de complexidade

Sem exceções constitucionais. A sessão persistida e a versão de conta dão suporte às regras de expiração, revogação e concorrência.
