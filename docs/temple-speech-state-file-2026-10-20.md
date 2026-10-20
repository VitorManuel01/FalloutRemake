# Salvamento separado do ensaio de Speech

`TempleSpeechStateFile` salva somente as variáveis do ensaio documentado em
`temple-speech-draft-2026-10-20.md`. O arquivo não é um checkpoint do mundo e não
altera o NPC, os itens, a experiência, as portas ou o progresso ativo.

- Formato v1, até 64 KiB, vinculado ao perfil e aos hashes das fontes locais.
- Aceita somente o estado inicial ou a aceitação das cinco escolhas verificadas.
- Campos obrigatórios, propriedades desconhecidas/duplicadas, versões, índices,
  fontes e valores inesperados são recusados antes de modificar uma sessão.
- Carregar cria um token novo, invalidando propostas anteriores. Uma falha mantém
  o estado e o token anteriores.
- Escrita em temporário próprio e substituição atômica: arquivo bloqueado mantém
  o salvamento anterior; o temporário é removido.
- Não contém texto original, mapas, imagens, bytecode ou outros assets do jogo.

Validação: 271 grupos sintéticos passaram, incluindo cinco novos grupos sobre
persistência. `--temple-state-report` confirmou ida e volta em memória com os dados
Epic instalados (`.local/temple-speech-persistence.json`), sem escrever um arquivo
de ensaio ou modificar os arquivos originais.

Próximo incremento: consulta explícita da conversa no cenário, carregando MSG da
instalação local e mantendo o ensaio separado do jogo. Modelo sugerido: GPT-6.1
Sol, esforço Alto.
