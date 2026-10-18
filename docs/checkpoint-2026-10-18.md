# Décimo primeiro marco — checkpoint local

Branch `codex/temple-local-checkpoint`, base `ef1e64f`.

**K / Salvar** registra a posição parada do personagem, área atual, portas e locais
do adaptador de cada área carregada. **R / Carregar** restaura a sessão, cancela
o percurso atual e reenquadra o personagem. Os botões ficam no cabeçalho.

Atalhos corrigidos em 18/10/2026: substituem F6/F9 para evitar conflito com o
editor. O usuário confirmou que F9 pausa a execução no Godot. A prévia não usa
mais essas teclas para checkpoint; não é preciso alterar as preferências do editor.
Revalidação da correção: compilação sem erros/avisos e testes Godot gráfico e sem
janela com saída 0. `CHECKPOINT_PREVIEW_OK` confirma K/R, botões e F9 ignorado pela
prévia. Captura `.local/temple-shortcuts.checkpoint.png` revisada; logs
`.local/shortcut-*.log`, sem erros no log gráfico. Isso verifica o aplicativo;
o atalho de pausa do editor permanece sob controle do Godot.

Slot normal: `.local/checkpoints/preview.json`. Não é um SAV original: não grava
na instalação do Fallout e não contém MAP, PRO, INT, imagens ou bytes de assets.
O arquivo de teste separado é `.local/checkpoints/preview-test.json`.

Formato versionado: perfil `neutral-v1`, versão 1, IDs, posição/rotação, chaves fonte,
flags de portas, oito locais conhecidos e recompensa única. Inclui hashes do catálogo,
MAP e dependências. O mesmo conjunto de dados pode estar em outro caminho; alterar
o conteúdo do catálogo ou das dependências torna o checkpoint incompatível.

A restauração monta outra `WorldSession`, valida todas as áreas, flags, locais,
XP e posição caminhável, e somente depois substitui a sessão ativa. Não permite
alterar scripts desconhecidos ou bits alheios à interação implementada. O JSON é
limitado a 1 MiB, 16 níveis de profundidade e campos conhecidos. O slot é substituído
por arquivo temporário no mesmo diretório, preservando o anterior se a escrita falhar.

Validação:

- **88 grupos sintéticos**, 8 novos para round-trip, formato, fingerprints, áreas,
  estado de portas, locais/XP, origem intacta e arrays independentes.
- Compilação sem erros/avisos; `CHECKPOINT_PREVIEW_OK` nos testes Godot gráfico e
  sem janela, saída 0. Captura 1440×900 revisada.
- Salvou no exterior com duas áreas, duas portas do templo abertas e 25 XP;
  reiniciou, carregou e retornou ao templo com o estado preservado.
- Teclas K/R e sinais dos botões validados. Catálogo incompatível recusado sem
  substituir o mundo ativo. Reconstrução das fontes byte a byte continua aprovada.
- O teste não toca no slot normal. Logs `.local/checkpoint-*.log`; captura
  `.local/temple-checkpoint.checkpoint.png`. Nenhum dado original entrou no Git.

Limites: registra apenas o estado implementado deste protótipo e o perfil fixo,
sem inventário, combate, relógio global, câmera ou migração de formatos antigos.
Reiniciar a prévia não apaga o slot; R pode recuperar o progresso salvo.
Próximo: reenquadramento e acompanhamento do personagem pela câmera.
