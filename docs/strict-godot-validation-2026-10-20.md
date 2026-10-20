# Logs do Godot como condição de aprovação — 20/10/2026

Durante o relógio manual, o Godot emitiu erros de nós fora da árvore, mas encerrou
com código zero e marcadores de sucesso. O problema visual foi corrigido naquele
marco. Os validadores agora também recusam `ERROR`, `SCRIPT ERROR`, exceção não
tratada e avisos de recursos/RID/ObjectDB vazados em stdout ou stderr.

O helper compartilhado exige marcadores no início de uma linha, evitando aceitar
uma mera menção ao nome dentro de outra mensagem. As mensagens deliberadas dos
testes de preferências bloqueadas/JSON inválido continuam permitidas, sem ocultar
erros reais do motor.

O runner isolado retorna `Passed` e `LogFailure`, mantém o código nativo e também
falha se o desktop de entrada mudou. Não tenta trocar o desktop nem abrir janela
normal como fallback. Cada modo exige os próprios marcadores.

Sete grupos de fixtures de logs verificam sucesso, falha deliberada de preferência,
erro nativo apesar do marcador, erro de script, exceção, vazamento e marcador
apenas mencionado. A bateria completa agora possui **14 etapas**: todas passaram,
junto dos **227 grupos sintéticos** de domínio.
Manifesto: `.local/temple-validation-20261020-105303-eaa65b0f/manifest.json`.
