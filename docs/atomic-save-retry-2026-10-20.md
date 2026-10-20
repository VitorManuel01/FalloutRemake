# Substituição atômica com recusa transitória — 20/10/2026

As baterias repetidas observaram “Access to the path is denied” em gravações
consecutivas das preferências. O processo externo responsável não foi identificado.
Preferências e checkpoint agora repetem somente a substituição atômica do arquivo
temporário quando o HRESULT corresponde a acesso/compartilhamento/bloqueio de
arquivo (5, 32 ou 33): quatro tentativas, com esperas de 10, 25 e 50 ms.

O arquivo anterior permanece íntegro até a substituição ter sucesso. Nenhum
formato ou política de leitura mudou. Erros não relacionados falham imediatamente;
um bloqueio duradouro continua recusado e a limpeza temporária existente permanece.

Dois novos grupos verificam recusa transitória seguida de substituição real e
limites/códigos de falha por injeção determinística. Os testes anteriores de arquivo
real bloqueado, preservação e limpeza continuam passando. Resultado: **227 grupos
sintéticos e 13/13 etapas da bateria completa**, incluindo o novo relógio manual.
Manifesto: `.local/temple-validation-20261020-104245-105e76a8/manifest.json`.
