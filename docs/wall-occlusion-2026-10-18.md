# Décimo quinto marco — revelar o personagem

Branch `codex/temple-wall-occlusion`, base `451636a`.

**V / Auto**, ao lado do filtro Paredes, ativa ou desativa a ocultação das paredes
que cobrem a base do personagem na projeção ortográfica. Vem ligada. O cálculo
acompanha movimento e câmera e restaura as malhas ao sair da linha de visão,
desligar a opção ou inspecionar outra elevação. Portas não são ocultadas.

Apenas a malha da parede muda de visibilidade. O objeto fonte, corpo, camada de
seleção, bloqueios e alcance continuam intactos; paredes ocultadas pela câmera
ainda podem ser inspecionadas. As contagens do painel representam os filtros,
incluindo os objetos temporariamente recortados na vista.

Segmento finito entre a projeção da câmera e a base do ator contra a caixa
provisória da parede, expandida por uma pequena margem. Não é geometria final do
asset original nem uma regra de visibilidade do Fallout. Sem alterar o material
compartilhado ou executar física a cada parede. Câmera/ator parados reutilizam o
resultado. As formas atuais são baixas, por isso poucas paredes precisam sumir.

Validação: **100 grupos sintéticos**, quatro novos para interseção finita, raios
paralelos, diagonais, limites e coordenadas inválidas. Compilação sem avisos/erros;
Godot gráfico e sem janela com `WALL_OCCLUSION_OK` e saída 0, log gráfico de erros
vazio. Caso real com abordagem alcançável, tecla V, botão, filtros, restauração,
troca de elevação, bloqueios, portas e reconstrução byte a byte. Captura
1440×900 `.local/temple-occlusion.occlusion.png` revisada.

Próximo: nomes e descrições originais dos objetos no inspetor, resolvidos por
mensagem do PRO e idioma disponível. Modelo: GPT-6.1 Sol, esforço Alto (High).
