# Identificação do baú do desafio na coleta

A janela J do baú conhecido explica que ele recebe itens do personagem por
script no jogo original e que desafio/recuperação ainda não estão disponíveis
nesta prévia. Recipientes desconhecidos conservam a explicação genérica.
O texto depende da identidade MAP, chave fonte, objeto e vínculo INT conhecidos.
Ele não libera acesso, cria conteúdo nem muda a propriedade dos itens.

O teste `SCRIPTED_CONTAINER_OK` verifica a mensagem específica, recusa de outro
perfil/objeto, limpeza da mensagem ao reutilizar o diálogo para caso genérico,
ações desabilitadas e ausência de alteração em personagem, itens, portas e MAP.

Build sem avisos/erros, integração headless e captura gráfica 1280×720 aprovados.
Runner isolado: `.local/chest-role-isolated.json`, `Passed=true`, logs sem erro,
desktop de entrada `Default` antes/depois. Captura verificada:
`.local/chest-role.scripted-chest.png`. O teste não abriu janela no desktop ativo.

Próximo incremento: retirar tudo dos recipientes estáticos já suportados,
checando elegibilidade e peso de todas as pilhas antes de qualquer transferência.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
