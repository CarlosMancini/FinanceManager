<!--
Sync Impact Report — material temporário de revisão; remover antes do commit.
Versão: 1.0.0 → 1.1.0 (MINOR: ampliação dos princípios e da governança).
Princípios modificados:
- II. Arquitetura modular → II. Arquitetura modular, DDD e MVC.
- VI. Critério de conclusão → VI. Testes com xUnit e critério de conclusão.
Seções adicionadas: Restrições técnicas e convenções; Fluxo de desenvolvimento e revisão.
Governança ampliada: precedência constitucional, emendas e versionamento semântico.
Seções removidas: nenhuma; títulos estruturais alinhados ao template ativo.
Pendências: TODO(RATIFICATION_DATE): confirmar a data original de adoção,
ausente na versão 1.0.0. Templates e planos não foram alterados neste fluxo.
-->

# FinanceManager Constitution

Escopo inicial pessoal e local.

## Core Principles

### I. Entrega guiada pela especificação

Cada feature tem um `spec.md` com comportamento e critérios observáveis, um `plan.md` com arquitetura, dados e verificação, e um `tasks.md` com tarefas ordenadas e rastreáveis. Antes de implementar, resolver as decisões que afetam os critérios de aceitação. Mudanças de comportamento atualizam os documentos e os testes na mesma entrega. Alterações a esta constituição registram versão, motivo e impacto nas features.

### II. Arquitetura modular, DDD e MVC

A aplicação DEVE manter uma interface web responsiva em Angular e TypeScript e uma API REST
em ASP.NET Core, com .NET 10 e C#. A persistência DEVE usar SQL Server via Entity Framework
Core (EF Core). Migrações DEVEM ser versionadas e verificadas em SQL Server. As versões
compatíveis das dependências DEVEM ser fixadas antes de iniciar o código.

A aplicação DEVE adotar um monólito modular, Domain-Driven Design (DDD) e arquitetura MVC.
O domínio DEVE conter as regras de negócio e as invariantes, sem dependências de HTTP,
EF Core ou detalhes de infraestrutura. Os serviços de aplicação DEVEM executar os casos
de uso e coordenar as operações de domínio e persistência. A infraestrutura DEVE
implementar as portas de persistência e integração.

Na organização MVC, os controllers DEVEM tratar a entrada e a saída HTTP, o modelo DEVE
representar os conceitos e comportamentos do domínio, e a interface Angular DEVE cumprir
o papel de apresentação. Todos os endpoints DEVEM delegar os casos de uso a serviços de
aplicação. Nenhum endpoint ou controller PODE acessar o banco diretamente, receber
`DbContext`, executar SQL ou chamar repositórios sem passar por um serviço de aplicação.
O servidor DEVE validar as regras de negócio em todos os pontos de entrada.

O código DEVE seguir Clean Code: nomes que expressem intenção, métodos e classes com
responsabilidade única, ausência de duplicação de regras de negócio e dependências
explícitas por injeção de dependência. Essas separações mantêm as regras testáveis e
impedem que detalhes de transporte ou persistência determinem o comportamento do domínio.

### III. Integridade financeira

- MVP em BRL. Usar decimal no domínio e decimal(19,2) para valores monetários persistidos. Valores informados devem ser positivos, ter até duas casas e caber no tipo persistido. A natureza da operação define o sinal. Na API, transportar valores como strings decimais invariantes. Calcular com precisão decimal e arredondar resultados monetários no ponto definido pela regra, com empate afastado de zero como padrão.
- Usar data civil para vencimentos e datas de movimentação. Avaliar o dia corrente em America/Sao_Paulo a partir de relógio injetável. Registrar instantes de auditoria em UTC.
- Distinguir obrigação, movimentação realizada e vínculo de baixa. A baixa integral exclui a obrigação da projeção e mantém uma única saída no realizado. Alterações, retries e concorrência devem preservar esse vínculo e a consistência dos totais.
- Toda alteração de estado financeiro composta deve ser atômica. Rastrear a origem dos dados e registrar alterações sensíveis com identificadores e metadados mínimos.

### IV. Segurança e privacidade

- A primeira versão atende a um titular e roda localmente, com acesso restrito à interface de loopback. Exigir autenticação e validar titularidade no servidor para toda leitura e alteração financeira. Aplicar HTTPS, cookies seguros e proteção CSRF nas operações que alteram estado.
- Manter credenciais, chaves e tokens fora do código e do repositório. Restringir acesso ao banco e aos backups. Excluir senhas, tokens, payloads bancários e dados financeiros dos logs. Usar dados sintéticos nos testes.
- Antes de receber dados bancários reais, definir retenção, revogação, descarte, proteção de tokens em repouso e procedimento de restauração de backup.

### V. Integrações substituíveis

Entrada manual, arquivos e agregadores convergem para o modelo interno por adaptadores. O domínio recebe dados normalizados. Importações devem ser repetíveis sem duplicar registros. No Open Finance, começar em sandbox, com escopos mínimos e leitura apenas. Registrar origem, conexão, identificador externo e estado da sincronização. Interromper a coleta quando o consentimento expirar ou for revogado. Qualquer integração real exige validar cobertura, acesso, custo e condições do provedor.

### VI. Testes com xUnit e critério de conclusão

A aplicação DEVE usar xUnit para os testes automatizados do backend. Os testes DEVEM
cobrir regras de domínio e casos de uso dos serviços de aplicação. Uma tarefa somente
termina quando os critérios associados passam por verificação e os testes relevantes
passam. Mudanças de comportamento DEVEM atualizar os testes na mesma entrega.

Cobrir regras monetárias e temporais, acesso indevido, falhas parciais e concorrência
conforme o risco da feature. Validar o fluxo pela interface e pela API e verificar
restrições de persistência e migrações com SQL Server. Testes unitários isolados não
substituem a verificação das características específicas do banco utilizado.

## Restrições técnicas e convenções

- Nomes de arquivos e classes DEVEM ser escritos em inglês e expressar sua finalidade.
- Mensagens de erro destinadas ao usuário, inclusive as retornadas pela API, DEVEM ser
  escritas em português. Códigos técnicos de erro DEVEM permanecer estáveis para os
  consumidores da API e não substituem a mensagem em português.
- Commits DEVEM seguir o formato `tipo(escopo): descrição do commit`, com tipo, escopo e
  descrição não vazios. Exemplo: `docs(constitution): define arquitetura e convenções`.

## Fluxo de desenvolvimento e revisão

Antes da implementação, o plano DEVE registrar como atende aos princípios desta
constituição, incluindo stack, separação de responsabilidades, persistência e testes.
As tarefas DEVEM refletir essas decisões e os critérios de aceitação da especificação.

Cada revisão de entrega DEVE verificar a conformidade constitucional, a passagem dos
endpoints pelos serviços de aplicação, as convenções de nomes e mensagens e os testes
relevantes. Conflitos encontrados DEVEM ser resolvidos antes de concluir a entrega.

## Governance

A constituição prevalece sobre especificações, planos, tarefas e demais orientações de
implementação. Se um plano contradizer a constituição, o plano DEVE ser corrigido.
A constituição NÃO PODE ser alterada apenas para acomodar um plano conflitante.
Verificar seus princípios antes de implementar e na revisão de cada entrega.

Emendas DEVEM ser propostas explicitamente, com motivo, impacto nos princípios e nas
features e indicação dos documentos que precisarão de adequação. A aprovação do
responsável pelo projeto DEVE preceder sua adoção. Cada emenda DEVE registrar a nova
versão e a data de alteração, preservando a data original de ratificação.

O versionamento DEVE seguir MAJOR.MINOR.PATCH: MAJOR para remoções ou redefinições
incompatíveis de princípios ou governança; MINOR para novos princípios, seções ou
ampliações materiais das regras; PATCH para esclarecimentos e correções sem mudança
de obrigação. A revisão de conformidade DEVE identificar artefatos divergentes para
correção, sem modificar templates ou planos durante o fluxo de atualização constitucional.

**Version**: 1.1.0 | **Ratified**: TODO(RATIFICATION_DATE): confirmar data original de adoção | **Last Amended**: 2026-11-09
