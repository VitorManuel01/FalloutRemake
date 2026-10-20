# Percurso explícito até o NPC

Selecionar o NPC conhecido da elevação 2 mostra **Chegar ao NPC**. A ação procura
o vizinho alcançável mais próximo, com desempate pelo hex, e pede a caminhada
normal. Não entra no hex bloqueado do NPC, não usa saídas e não abre conversa
automaticamente. O modo Caminhar é habilitado para mostrar o percurso.

Porta final fechada, outra elevação, caminhada em andamento, janela modal ou
ausência de vizinho alcançável recusam a ação. Chegada ao alcance desabilita
novo percurso e habilita **Conversa · ensaio**. O pedido não modifica o mundo
antes de a caminhada avançar; somente o marcador do jogador percorre a rota.

Validação: build sem avisos/erros; `TEMPLE_NPC_APPROACH_OK` na cena headless e
na captura isolada a 1280×720 (`.local/npc-route-isolated.json`). Conferidos
recusa pela porta fechada, rota exata de 32 passos a partir da passagem, bloqueio
durante a caminhada, chegada ao vizinho 13928, repetição sem movimento adicional,
ausência de abertura automática da conversa, itens/XP e origem preservados.
Enquadramento pela ação existente **Ver percurso** passou na captura.
O desktop de entrada permaneceu `Default` e os logs não contêm erros.

Próximo: conferir os efeitos posteriores à aceitação de Speech no original,
especialmente movimento, recompensa e repetição, antes de integrar progresso à
partida. GPT-6.1 Sol, esforço Alto.
