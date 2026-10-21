# Primeira integração dos modelos Nature

Branch `codex/nature-assets`. Usa somente arquivos locais de `Assets/Nature`.
O conteúdo de `Assets/` permanece fora do Git; os modelos importados e suas
texturas não são copiados, reexportados ou modificados.

O inventário GLB verificou cinco modelos: RiverRock1 (64 triângulos), RockS01
(93), BlastedForestBurntTreeStump01 (1136), BlastedForestBoulder01 (2088) e
BlastedForestCliff01 (4154). Eles já possuem materiais/texturas PBR embutidos;
o importador local gera LODs. `tools/InspectNatureAssets.py` produz somente
metadados e recusa referências externas que escapem de Nature.

Esta etapa aplica **RiverRock1 e RockS01** a sete pares PID/FID originais de
rocha: **23 instâncias no exterior do templo**, **30 na vila de Arroyo**.
Os modelos são compartilhados em **três lotes de malha por área**, sem
instanciar uma cena completa para cada pedra. Escala uniforme ajusta a largura;
o apoio no chão e a rotação vêm do objeto original. Materiais, texturas e
geometria dos arquivos importados são reutilizados.

O interior do templo tem bancos, não rochas compatíveis. Árvores completas,
plantas, monumentos e bancos não são substituídos por pedras ou troncos.
Paredão, pedregulho e tronco ficam disponíveis no inventário técnico, ainda sem
correspondência visual aprovada nesta etapa. Nenhuma outra categoria é carregada.

**Nature 3D**, no painel lateral, compara os modelos com as formas provisórias.
Filtros também ocultam os lotes; desligar restaura a visibilidade anterior de
cada peça provisória. Identidade, posição do objeto, seleção, colisão de
consulta, navegação, itens, checkpoint e scripts permanecem independentes da
apresentação. Se um modelo estiver ausente, permanece a forma provisória.

Verificação: build sem avisos/erros, teste headless `NATURE_MODELS_OK`, comparação
de checkpoint antes/depois, seleção pelos IDs originais e filtros. Capturas
isoladas 1280×800 aprovadas em `.local/nature-isolated.json`, desktop de entrada
`Default` preservado; imagens `.local/nature-1280.nature-map-126.png` e
`.local/nature-1280.nature-map-4.png`. Inventários locais:
`.local/nature-assets-inventory.json` e `.local/nature-source-report.json`.
Bateria completa aprovada: **311 grupos sintéticos**, **19/19 etapas**,
`.local/temple-validation-20261021-110239-115642a8/manifest.json`.

Próximo marco: separar entrada efetiva do cache de mapas e conceder uma única
vez o prêmio de Speech comprovado na chegada a Arroyo. Modelo recomendado:
**GPT-6.1 Sol, esforço Alto**.
