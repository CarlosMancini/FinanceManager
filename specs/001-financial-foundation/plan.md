# 001 · Plano técnico

## Resumo

Acessar a aplicação local como titular e cadastrar contas financeiras que servirão de origem ou destino das movimentações.

## Contexto técnico

Angular e TypeScript no frontend, ASP.NET Core e EF Core no backend, SQL Server na persistência. Consultar o plano da fundação para o arranjo da solução.

## Verificação da constituição

Identidade, proteção CSRF, titularidade e persistência em SQL Server. Aplicar os critérios globais da [constituição](../../.specify/memory/constitution.md).

## Estrutura do projeto

A documentação da feature fica nesta pasta. Código em "src/backend" e "src/frontend", com testes em "tests".

## Estratégia de implementação

## Estrutura

Organizar o código em backend ASP.NET Core com camadas API, Aplicação, Domínio e Infraestrutura, frontend Angular e testes. Usar EF Core e migrações versionadas em SQL Server. Servir o frontend e a API na mesma origem HTTPS. No desenvolvimento, encaminhar chamadas por proxy local.

## Identidade e dados

Usar ASP.NET Core Identity com cookie seguro e HttpOnly, proteção CSRF para mutações e limitação de tentativas de login. Provisionar um titular por comando local, com senha inicial fornecida por entrada segura. Desativar cadastro público. Derivar `OwnerId` da sessão. Criar `FinancialAccount` com ID, titular, nome, tipo, moeda e estado ativo/arquivado. Limitar nome a 120 caracteres após remoção de espaços nas extremidades. Padronizar erros da API com Problem Details.

## Verificação

Validar login e logout, cookies e CSRF, isolamento por titular com identidades sintéticas em testes e ciclo de arquivamento/reativação. Executar migrações e checar persistência em SQL Server. Registrar no repositório os pré-requisitos e os passos para inicialização local quando implementar.
