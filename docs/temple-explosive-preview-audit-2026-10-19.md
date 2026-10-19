# Marco 58 — ensaio integrado com coleta real na prévia

O teste da representação posicionada agora percorre as portas e a passagem para
a elevação 1 na prévia Godot. Coleta a unidade original 364/PID 85 do pote 358
pela operação normal de coleta, chega ao alcance da barreira e faz o commit de
posicionamento. Substitui a fixture do marco 55 que começava com a unidade já
carregada. O sucesso do arrombamento e do temporizador é controlado pelo teste;
não há combate, armadilhas gerais ou execução arbitrária de scripts.

Depois de 175 ticks lógicos, o teste salva, reinicia e carrega: GUID e 425 ticks
restantes persistem. Inspecionar outra elevação oculta a forma sem mover o ator;
voltar não duplica a instância. Em zero, a unidade continua posicionada e a
barreira ainda bloqueia. A proposta e a projeção reconhecem o alvo e a alternativa
de 1258 hexes, sem alterar a navegação real. A captura mostra explicitamente que
nenhum efeito foi aplicado. A conclusão lógica ao final é apenas limpeza do
ensaio, sem abertura da barreira; o reset restaura toda a sessão de teste.

`TEMPLE_EXPLOSIVE_PREVIEW_AUDIT_OK` integra os marcadores obrigatórios da bateria.
Continuam sendo verificados filtro Cenário, posição no referencial original,
identidade separada dos registros MAP, ausência de colisão, contagens, origem,
conservação de quantidades e reconstrução byte a byte. Só o slot de teste é usado.

Capturas sempre por `Run-QuietGodot.ps1`, sem trocar o desktop de entrada.
O ensaio não adiciona botão de ativação nem liga o relógio ao tempo real.

Próximo: checkpoint de substituições e commit atômico do efeito antes de liberar
o uso pela interface. GPT-6.1 Sol, esforço Alto.
