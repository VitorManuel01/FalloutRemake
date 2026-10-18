# Caminhada inicial por hexágonos

O modo **M / Caminhar** move um marcador azul na elevação 0 de `ARCAVES`. Clique no
chão; o percurso aparece em âmbar. Durante um percurso, novos destinos são recusados
até chegar ou reiniciar. **Reiniciar prévia** cancela o percurso, volta à entrada e
restaura as portas. Trocar de elevação reinicia o marcador e preserva o estado das
portas. F apenas reenquadra a entrada na mesma elevação.

## Regras da fotografia inicial do mapa

`HexNavigation` é C# sem Godot. A consulta é equivalente à regra de ocupação de
`_obj_blocking_at` do CE para um marcador de **um hex**, usando os objetos raiz
serializados. Não executa a preparação do mapa nem seus scripts.

| Dado | Efeito |
| --- | --- |
| FID: `(fid >> 24) & 15` igual a 1, 2 ou 3 | Criatura, cenário ou parede pode bloquear. PID não decide esse tipo. |
| `HIDDEN` (`0x1`) ou `NO_BLOCK` (`0x10`) | Objeto não bloqueia. São flags fonte, independentes dos filtros da interface. |
| `MULTIHEX` (`0x800`) | Bloqueia também os seis vizinhos, respeitando bordas e paridade. |
| Elevação/hex fonte | Apenas a elevação consultada; hex `-1` não é posicionado. |
| Objetos sobrepostos | Todas as causas preservadas por chave `hash:offset`, inclusive IDs repetidos. |
| Inventário | Não produz ocupação no mapa. |
| Piso/teto | Não define passabilidade. Não há máscara de piso nem uso das caixas de seleção. |

Portas usam os flags de ocupação gravados. `openFlags` bit 0 indica estado aberto;
bit `0x02000000` indica tranca. Desacordos entre estado aberto e `NO_BLOCK` são
contados no relatório, sem corrigir a fonte. Não houve desacordos na elevação 0.
O CE pode permitir uso automático de determinadas portas; **isso não foi implementado**.
Uma porta fechada sem `NO_BLOCK` permanece bloqueadora até ser explicitamente aberta.
A [interação com portas](doors-2026-10-17.md) usa estado de sessão separado da fonte,
fornecendo flags efetivos à mesma consulta de navegação.

Grades de saída visíveis, PIDs `0x05000010`–`0x05000017`, são destinos terminais:
chegar é permitido, atravessar para continuar buscando caminho não é. Ao chegar,
o marcador para; reinicie para sair. Essa é uma restrição explícita da prévia,
pois ainda não há transição de mapa ou elevação.

## Busca e movimento

Busca em largura em até 40.000 hexes, seis vizinhos por célula, custo uniforme de
um passo. Não reproduz prioridades de giro, pontos de ação, perigos ou preferências
do caminho original. Uma célula pode estar livre e ainda ser inacessível desde a entrada.
Somente as alcançáveis são verdes. Bloqueios aparecem em vermelho, saídas alcançáveis
em âmbar. O marcador usa cinco passos por segundo e interpola entre centros adjacentes;
seus hexes permanecem discretos e a fonte nunca é alterada.

`FalloutGrid.HexAt` converte o clique no plano do chão para o hex mais próximo,
incluindo células virtuais externas para não prender cliques na borda do mapa.
O teste cobre a ida e volta de todos os 40.000 índices.

## Resultado local e limite

Elevação 0: **1.206 hexes bloqueados**, **540 alcançáveis** desde 28906, **31 hexes de
saída** (13 alcançáveis), com a primeira porta fechada. A porta ID 1482, hex 24308,
começa fechada. Sua aproximação
no hex 24508 exige 23 passos; a saída 31306 exige 12. A porta ID 102, hex 11108, fica
além do trecho alcançável com a primeira porta fechada.

Abrir a primeira porta aumenta o alcance a **2184 hexes**. Não há IA, combate,
destrancamento, armadilhas ativas, execução de scripts, EDG, personagem
multihex ou comparação visual lado a lado com o jogo original. O resultado descreve
os dados serializados, não uma simulação completa do estado após executar os scripts.

Foi confirmado que a primeira porta não tem script. A segunda está associada a
`doorl100.int`; suas [condições já foram mapeadas](door-script-2026-10-18.md).
O [adaptador de sessão](door-adapter-2026-10-18.md) já valida inicialização e Lockpick
em um cenário determinístico separado. A prévia ainda usa o perfil sem esse adaptador.

Referência local [Fallout 2 CE](https://github.com/alexbatalov/fallout2-ce/tree/e97087b9582f37075db347a89898887320753f8b):
`object.cc` (`_obj_blocking_at`), `obj_types.h` (FID/flags), `proto_instance.cc`
(`_check_door_state`, `objectIsLocked`) e `animation.cc` (`canUseDoor`, `pathfinderFindPath`).
