# Consulta da conversa original no cenário

Ao chegar a um hex do NPC da elevação 2 e selecioná-lo, **Conversa · ensaio**
abre uma janela modal. A ação fica indisponível durante a caminhada, em outra
elevação, longe do NPC ou sem os dados originais necessários. Outros objetos
não mostram esse botão.

A janela carrega `text/<idioma>/dialog/actemvil.msg` da mesma língua usada pelos
textos de objetos; exige todos os IDs do percurso e não mistura idiomas. Mostra
as cinco escolhas documentadas no perfil Speech, com INT 5/tag ativo, mantendo
o perfil real do personagem e o checkpoint da partida intactos. O resultado
pertence somente ao banco separado do ensaio. Fechar cancela; reabrir e recomeçar
retornam ao início. Nenhum texto ou outro asset original entra no Git.

Revisão visual encontrou e corrigiu a apresentação dos pares 103/104, 122/123
e 166/167: são fragmentos unidos por `obj_name(dude_obj)`, não alternativas
aleatórias. O perfil agora verifica as instruções físicas dessas composições
e das respostas simples. A janela mostra `[nome do personagem]` explicitamente,
pois o protótipo ainda não tem nome definido. Não inventa nome ou simula RNG.

Validação: build Godot .NET sem avisos/erros, 273 grupos sintéticos, integração
headless e duas capturas isoladas a 1280×720. A segunda confirmou a composição
corrigida, leitura, escolha, cancelamento/reabertura, reinício, repetição final,
isolamento da câmera e igualdade integral do checkpoint ativo em cada escolha.
`TEMPLE_SPEECH_UI_OK` passa a ser obrigatório na bateria e no runner pertinente.
Os manifestos em `.local/speech-reviewed-isolated.json` confirmam desktop de
entrada `Default` antes/depois e ausência de erros nos logs.

Limites: sem fala ativa do NPC, movimento, XP, duelo, transferência de inventário
ou execução geral de INT. O arquivo separado do ensaio existe como API validada;
esta primeira janela ainda não oferece salvamento/carregamento.

Próximo: persistência explícita do ensaio pela janela, incluindo restauração e
recusa de arquivos incompatíveis sem tocar na partida. GPT-6.1 Sol, esforço Alto.
