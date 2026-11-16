# Contrato de interface · 004

Rotas são decisões deste plano para implementar FR-001–017, sem novas funções de perfil.

| Rota/estado | Comportamento |
|---|---|
| `/login`, sem sessão | E-mail, senha oculta, Entrar e Criar conta. Enquanto há legado incompleto, oferecer **Usar acesso antigo** com campos de nome de acesso/senha para a prova, na própria tela. |
| `/register`, qualquer instalação | Apresentar imediatamente o formulário com nome, apelido opcional, nascimento, e-mail, telefone, senha e confirmação, regras visíveis e opção Voltar ao login, sem consulta de contas existentes na abertura. Pesquisar duplicidade de e-mail somente no envio válido. |
| `/register`, legado pendente | Orientar **Completar cadastro existente** e pedir credenciais antigas; manter formulário de novo usuário disponível, sem mostrar dados financeiros/pessoais existentes. |
| `/register`, outros cadastros concluídos | Exibir formulário de novo usuário; no envio de e-mail duplicado, informar o conflito e manter dados corrigíveis. |
| `/register/complete`, autorização válida | Mesmo formulário de dados obrigatórios, exibindo orientação sobre o prazo; sem acesso financeiro ou dispensa de regras. |
| `/register/complete`, sem autorização/expirada | Pedir prova das credenciais antigas; conservar em memória os campos não secretos já preenchidos. Prova nova inicia outro prazo. |
| Cadastro ou conclusão com sucesso | Confirmar em português, limpar senhas e ir a `/login`; não preencher senha nem autenticar automaticamente. |
| Sessão válida, cadastro concluído | Manter navegação autenticada existente; entrada após login segue MOV-05. |

Para entrada autenticada: nenhuma conta financeira → cadastro de conta; alguma conta, inclusive somente arquivadas → histórico. Contas arquivadas não habilitam novas movimentações. Cadastro incompleto sempre prevalece sobre navegação financeira, inclusive para cookies emitidos antes da atualização.

## Formulários e falhas

- Associar label/erro a cada campo, indicar obrigatórios, resumo de erros com foco e status de envio acessível; interação por teclado e layout responsivo. Mensagens em português.
- Mostrar critérios de senha antes do envio e destacar cada categoria/limite não atendido. Contar pontos de código, preservar espaços/caixa, ocultar por padrão; `autocomplete=new-password` no cadastro e `current-password` no acesso/prova.
- Guardar dados não secretos somente no componente/estado em memória enquanto o formulário permanece aberto. Não persistir perfil/senhas em navegador; abandonar a tela descarta senhas. Senha antiga da prova não se torna senha nova nem vai para login.
- Em validação, manter dados não secretos e erros junto aos campos; falha de gravação não confirma sucesso. Estado ocupado impede duplo clique, sem substituir proteções SQL de concorrência.
- Expiração absoluta é aplicada pelo servidor; relógio visual não é autoridade. Evitar polling que renove sessão ou autorização. Autorizações retornadas pelo servidor nunca habilitam guards financeiros.
- Em resposta perdida, manter formulário e orientar nova tentativa ou login. Estado global não comprova sucesso individual, pois outros usuários podem existir. A resposta de conflito/replay do servidor orienta a repetição sem sobrescrever dados.
- Guards e restauração distinguem sessão financeira válida de cadastro pendente. Interceptor cobre também `/api/bills`; erros de prova/conclusão não entram num ciclo de redirecionamento genérico de sessão expirada.

Telefone aceita somente dígitos na edição/colagem e aplica máscara progressiva `(DD) XXXXX-XXXX`; comprimento diferente de 11 dígitos gera erro, sem truncar silenciosamente excedentes. Confirmação de senha é oculta, obrigatória e deve coincidir exatamente com senha válida. Descartar ambos os campos secretos ao sair/sucesso.

## Regressões obrigatórias

Comprovar acesso protegido às telas e APIs de contas, movimentações e obrigações; saída e trinta minutos de inatividade; retorno ao login por expiração; preservação do histórico após migração; ausência de senhas ao voltar/abandonar/sucesso e ausência de dados pessoais no console/logs.
