# Caminhada até a anciã original

Em Arroyo, **Ir até a anciã** seleciona a identidade original e caminha até o
hex vizinho alcançável mais próximo. A ação não atravessa saídas, move o NPC,
teleporta o jogador ou abre conversa. Ao chegar, mostra **Anciã ao alcance**.
Os obstáculos e portas do mapa continuam determinando o caminho.

A identificação exige MAP/hash, offset **126588**, chave de origem, PID/FID,
SID **67108867**, script **10** e binding/hash de `ahelder.int`. O ID 20
isoladamente não é usado porque outro NPC possui o mesmo número. Inspecionar
a orientação não carrega mapas ou altera o checkpoint. O percurso devolvido
é uma cópia; chamadas antigas após carga/reset não podem iniciar caminhada.

Validação: **326 grupos sintéticos**, builds sem avisos/erros, cenário Godot
headless com `ARROYO_ELDER_APPROACH_OK` e captura isolada aprovada em 37,3 s.
O teste real interrompe no meio de um passo, salva/carrega, recusa callback
antigo, recalcula o percurso, chega adjacente e restaura o checkpoint da
chegada. XP **625**, reputação **65**, inventário e progresso do templo ficam
iguais. A reconstrução de todos os MAPs é verificada novamente.

Evidências locais: `.local/arroyo-elder-route-synthetic.log`,
`.local/arroyo-elder-route-headless.log`, `.local/arroyo-elder-isolated.json`,
`.local/arroyo-elder-1280.arroyo-elder-arrival.png`. Desktop de entrada
`Default` preservado antes/depois; nenhuma janela normal foi aberta.

Próximo marco: consulta separada dos textos originais da abertura, com
guardas de alcance/sessão e sem conceder itens ou registrar uma conversa
ativa enquanto o preâmbulo de reação não estiver implementado.
Modelo recomendado: **GPT-6.1 Sol, esforço Alto**.
