# Modelo local ausente e recuperação

Se um modelo Nature não estiver disponível, somente os objetos correspondentes
mantêm as formas provisórias e seus volumes de seleção originais. Os demais
modelos permanecem renderizados. O tooltip de **Nature 3D** informa quantos
objetos estão usando a forma provisória por falta do modelo local.

Uma ausência em cache agora é consultada novamente durante a reconstrução da
cena. Quando o modelo importado volta a ficar disponível, a malha e a seleção
por triângulos são recuperadas. Modelos já carregados continuam compartilhados;
esta mudança não implementa recarga automática de um arquivo que foi editado.

O teste simula ausência de RockS01 no leitor de disponibilidade, sem renomear,
mover ou modificar os arquivos do usuário. Confere a contagem exata de objetos
afetados, o volume provisório ativo, os mesmos IDs, as outras malhas e a
igualdade do checkpoint. Depois restaura a disponibilidade e reconstrói a cena
para comprovar a recuperação do cache negativo. O teste revelou e corrigiu
uma colisão de chave ao substituir a entrada ausente pelo modelo recuperado.

Build sem avisos/erros; `NATURE_ASSET_FALLBACK_OK` e captura isolada aprovados em
`.local/nature-recovery-isolated.json`, 1280×800, desktop `Default` preservado.
Nenhum asset ou arquivo da partida foi regravado por esse teste.
Bateria final: **321 grupos sintéticos**, **19/19 etapas**, builds sem avisos/
erros, `.local/temple-validation-20261021-112858-3e249650/manifest.json`.
