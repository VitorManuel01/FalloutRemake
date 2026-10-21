# Arquivo separado da saída do NPC

`TempleNpcDepartureFile` salva apenas versão, fingerprint do perfil, fase,
índice do percurso e ticks restantes. Posição, rotação, bloqueador, porta e
visibilidade são reconstruídos desse registro único. Não armazena os percursos,
textos, modelos ou outros dados originais.

A leitura exige todos os campos, recusa nomes duplicados/desconhecidos, tipos
inválidos, fases incompatíveis, fontes diferentes e arquivos maiores que 8 KiB.
Um candidato só substitui a sessão após validação completa, com token novo.
A gravação usa arquivo temporário próprio e substituição atômica; falhas mantêm
o arquivo anterior e removem o temporário dessa operação.

Validação: 285 grupos sintéticos, incluindo round-trip de cada hex e de cada
tick da espera, estado final, falhas de leitura sem mutação e destino bloqueado.
O relatório real também passa a serializar e decodificar cada um dos 93 estados
restaurados. O checkpoint da partida e o arquivo Speech v1 permanecem separados.

Próximo: controles manuais e traçado visual do ensaio na consulta Speech.
