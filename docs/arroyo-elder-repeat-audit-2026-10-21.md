# Conversa repetida e conhecimento de áreas

Após uma saudação concluída pelos caminhos auditados, L4 já vale 1. Mantidas
as guardas de vila, tempo, INT e ausência de recusa, o original seleciona
**Node004**, resposta **117**, e não repete Node001.

Esse procedimento consulta `metarule(17,11)`. No Community Engine, regra 17
chama **wmAreaVisitedState**, não uma simples consulta ao mapa carregado.
Ela devolve visitedState somente quando a área é conhecida/visível e foi
visitada. O **ID de área 11 é Vault 13**, resolvido em `data/city.txt` da
instalação local. Começa `Off`, com visitedState zero inicializado pelo motor.
Isso é distinto de `WorldSession.EnteredMaps`, que armazena IDs de **MAP**.

Fonte resolvida: `data/city.txt`, `patch000.dat`, 17983 bytes, SHA256
`953B218E60F5BD281A06870D35B13A45CA6953629F325748E55DCF9B03797231`.
O novo auditor exige esse perfil e confere as instruções e opções de Node004:

- 118→Node005 somente quando a área 11 retorna estado não zero.
- L7=0: 119→Node015 e 121→Node013; além de 122→Node007 e 123→Node999.
- L7!=0: 120→Node009 substitui 119/121; 122/123 continuam disponíveis.
- Node015 permanece fora do runtime, pois cria/adiciona item, transfere moeda
  e escreve progresso. Os caminhos do ensaio atual deixam L7=0.

Nenhuma informação de área foi inferida das visitas aos mapas 3/4/126. A
conversa repetida ainda não é executada ou oferecida como conversa ativa.
Validação real: `ARROYO_ELDER_REPEAT_AUDIT_OK`, em
`.local/arroyo-elder-repeat-audit.json`, compilação sem avisos/erros.

Fontes: [interpreter_extra.cc](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/interpreter_extra.cc)
e [worldmap.cc](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/worldmap.cc),
além do INT/city.txt originais locais. Apenas IDs e evidências técnicas foram
registrados; os arquivos proprietários não foram exportados para o Git.

Próximo marco: estender o ensaio separado para a conversa repetida neutra,
com área 11 inicialmente desconhecida e L7=0, antes de persistir estado ativo.
Modelo recomendado: **GPT-6.1 Sol, esforço Alto**.
