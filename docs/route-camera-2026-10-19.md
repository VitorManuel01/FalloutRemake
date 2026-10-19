# Vigésimo terceiro marco — enquadrar o percurso

Branch `codex/temple-route-camera`, base `be61e56`.

Concluído em 19/10/2026, mantendo a validação isolada iniciada no marco anterior.

No modo Caminhar, T ou **Ver percurso** ajusta a câmera ortográfica para mostrar
o caminho inteiro, o personagem e, nas aproximações, a porta de destino. Usa o
espaço acima do painel de navegação e à esquerda do inspetor. C retoma o
acompanhamento do personagem; enquadrar não altera posição, progresso ou destino.

O botão conserva o último percurso válido enquanto o ponteiro vai ao painel.
Depois do enquadramento, a prévia fica visível até o ponteiro se deslocar mais de
4 pixels. Apontar outro hex, iniciar uma interação, trocar de camada/área ou
reiniciar invalida a prévia antiga. Sem percurso válido, a ação fica desativada.

`OrthoFraming` calcula limites no plano da câmera, margem e deslocamento para a
região livre. A projeção atual fornece a escala em cada eixo, inclusive após
girar a câmera. O resultado é conferido pela projeção real do Godot. Percursos
que excedem o limite de zoom ou uma região livre inválida preservam a vista
anterior e exibem uma mensagem; não se apresenta um enquadramento cortado.

Validações realizadas:

- Compilação .NET sem avisos ou erros; 118 grupos sintéticos aprovados.
- Integração Godot sem janela e gráfica no desktop isolado, ambas com saída 0.
- T e botão; trajeto real de 23 passos; restauração após passar pelo painel;
  rota com câmera girada; aproximação incluindo a porta; preservação do avanço
  durante a caminhada; ação indisponível sem rota ou após conclusão/inspeção.
- Portas e reconstrução dos bytes do mapa conferidas sem alterações.
- Captura `.local/temple-route-camera.route-frame.png` revisada em 1440 × 900.
  Dados e imagens originais continuam apenas locais, fora do Git.

Logs: `.local/route-camera-build.log`, `.local/framing-synthetic.log`,
`.local/route-camera-preview.log`, `.local/temple-route-camera.stdout.log` e
`.stderr.log`. O resultado isolado registra o desktop de entrada antes/depois;
a execução não troca o desktop do usuário.

Limites: enquadra o traçado, sem calcular visibilidade entre todos os objetos;
o recurso existente de ocultação de paredes continua independente. Interface
validada na resolução atual de 1440 × 900. Sem novos scripts de jogo ou combate.

Próximo incremento sugerido: guardar as preferências visuais localmente, para
reabrir com a mesma grade, piso e ocultação de paredes. Modelo recomendado:
GPT-6.1 Sol, esforço Alto (High).
