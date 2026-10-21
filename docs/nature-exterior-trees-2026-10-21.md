# Árvores Nature no exterior do templo

As **88 árvores originais de ARTEMPLE (mapa 126)** agora usam o modelo local
`Assets/Nature/BlastedForestBurntTreeStump01.glb`. É uma árvore queimada/tronco
com raízes, sem copa ou folhas. Nenhum modelo de outra categoria foi carregado.
As árvores de Arroyo Village permanecem com a representação anterior.

Correspondências restritas: PID/FID **33555377/33555388**,
**33555378/33555389** e **33555379/33555390**, somente no mapa 126, sem script
ou inventário. Escala uniforme dá largura de 1,25 e altura aproximada de 2,21
unidades. Posição e rotação vêm do registro original. Nenhum objeto novo foi
inserido no mapa e os bloqueios originais permanecem.

As 88 árvores compartilham uma malha de **1136 triângulos** e seus materiais
PBR. O exterior agora possui **quatro lotes**: três das pedras e um das árvores.
**Nature 3D** compara toda essa apresentação com as formas provisórias; filtros
e seleção continuam ligados ao corpo original. O inspetor acompanha a opção,
descrevendo a árvore queimada quando visível e a placa provisória ao desativar.

Captura isolada aprovada em `.local/nature-trees-isolated.json`, 1280×800,
entrada `Default` preservada, logs limpos. Imagem:
`.local/nature-trees-1280.nature-exterior-trees.png`. Testes verificam as 88
identidades, seleção, correspondências por área, filtros, comparação, lotes e
igualdade do checkpoint. Assets e texturas continuam exclusivamente locais.
Bateria completa aprovada: **317 grupos sintéticos**, **19/19 etapas**, builds
sem avisos/erros, `.local/temple-validation-20261021-111159-1921ba88/manifest.json`.

Depois deste marco, o fluxo normal retoma a validação de retorno real entre
Arroyo e o templo, garantindo que a recompensa não se repita. Modelo
recomendado: **GPT-6.1 Sol, esforço Alto**.
