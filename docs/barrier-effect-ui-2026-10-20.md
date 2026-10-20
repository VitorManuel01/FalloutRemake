# Resolução da barreira pela interface — 20/10/2026

Depois do vencimento, **Tempo · pendente → Ver efeito** consulta o evento
selecionado. O perfil original do Templo e seus PRO/INT/MAP continuam conferidos
pelas propostas existentes. A revisão mostra a passagem e a área alcançável antes
e depois; abrir/cancelar essa revisão não altera propriedade ou cenário.

**Aplicar no cenário** revalida o plano e executa o commit atômico existente:
consome a unidade posicionada, cria uma substituição com GUID próprio e reconstrói
a navegação/apresentação. O objeto fonte permanece registrado, com seus IDs e
bytes intactos; somente sua apresentação/bloqueio é substituída na sessão.
K/R preservam a unidade consumida e os destroços pelo checkpoint v4.

É um ensaio de cenário, explicitamente informado na revisão: não aplica dano
a criaturas ou ao personagem. Evento desconhecido, perfil indisponível, alvo
fora do alcance, barreira já aberta ou plano obsoleto não são descartados.
A aplicação exige ator parado na área/elevação da barreira. Fechar pelo X ou
cancelar retorna à fila; carregar/reconstruir invalida a janela e seus callbacks.

`BARRIER_EFFECT_UI_OK` cobre revisão sem alteração, cancelamento/X, atalho do
mundo bloqueado, evento desconhecido, callback após carregar, confirmação e
repetição, consumo único, um destroço, antigo hex caminhável, identidade fonte,
quantidade da bolsa/XP inalterados e round-trip exato de checkpoint.
O teste começa com a unidade original coletada e posicionada pela interface.

A bateria completa exige o novo marcador. Captura isolada em 1280×720 passou
com código zero e saída de erro vazia; desktop de entrada preservado. Nenhum
asset original foi adicionado ao Git.

Resultado: build sem avisos/erros, 227 grupos sintéticos e 13/13 etapas completos.
Manifesto: `.local/temple-validation-20261020-104632-6d4f16a1/manifest.json`.

Próximo: orientar o posicionamento dentro do alcance conhecido e oferecer
aproximação da barreira pela bolsa, usando a navegação validada.
GPT-6.1 Sol, esforço Alto.
