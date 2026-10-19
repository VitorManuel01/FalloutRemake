# Validação durante o uso simultâneo do computador

Por solicitação do usuário em 18/10/2026, o Godot não deve interferir no uso do
computador enquanto ele realiza outros testes. Esta preferência vale até o
usuário alterá-la.

- Use `--headless` para validação sem imagem.
- Para capturas, use `tools/Run-QuietGodot.ps1`: ele inicia somente a instância de
  teste em um desktop isolado, sem trocar o desktop de entrada.
- Não abra o editor ou o jogo no desktop ativo. `Start-Process -WindowStyle Hidden`
  sozinho esconde o console, mas não garante isolar a janela criada pelo Godot.
- Se o runner isolado falhar, continue as verificações sem janela e registre a
  limitação; não abra automaticamente uma janela normal como fallback.
- Não altere, mova, minimize, encerre ou envie entrada às instâncias do usuário.
  Uma abertura em monitor específico pode ser feita se o usuário a solicitar.
