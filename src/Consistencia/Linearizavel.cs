namespace Conde.Consistencia;

/// <summary>
/// O JUIZ deste repositorio: a busca EXAUSTIVA por uma linearizacao.
///
/// A definicao de Herlihy e Wing, de 1990, em uma frase: uma historia e
/// linearizavel se existe uma ordem TOTAL das operacoes que
///
///   1. respeita o tempo real (se A terminou antes de B comecar, A vem antes), e
///   2. e aceita pela especificacao sequencial.
///
/// Repare que a definicao e um EXISTE. Nao da para verificar olhando a historia:
/// tem que procurar a ordem. Esta classe procura todas, e por isso ela e
/// exponencial e exata.
///
/// O algoritmo e o de Wing e Gong: em cada passo, qualquer operacao que ja possa
/// vir primeiro (nenhuma das que sobraram terminou antes dela comecar) e
/// candidata. Tenta, recursa, desfaz. Sem heuristica, sem poda esperta: a unica
/// poda e a propria especificacao recusar.
/// </summary>
public static class Linearizavel
{
    /// <summary>
    /// A historia e linearizavel em relacao a especificacao.
    ///
    /// O limite existe porque a busca explode, e explodir e honesto: as historias
    /// das medidas tem poucas dezenas de operacoes, que e exatamente o tamanho em
    /// que uma violacao ja aparece e a busca ainda termina.
    /// </summary>
    public static bool Eh(Historia historia, Especificacao especificacao, int limite = 20)
    {
        ArgumentNullException.ThrowIfNull(historia);
        ArgumentNullException.ThrowIfNull(especificacao);

        if (historia.Quantas > limite)
            throw new ArgumentException($"a busca exaustiva so cabe em ate {limite} operacoes", nameof(historia));

        return Ordem(historia, especificacao) is not null;
    }

    /// <summary>
    /// A ordem que linearia a historia, ou nada se nao existe.
    ///
    /// Devolver a ordem, e nao so um sim ou nao, e o que torna a resposta
    /// conferivel: o teste refaz a execucao sequencial naquela ordem e verifica
    /// que a especificacao aceita cada passo, sem confiar na busca.
    /// </summary>
    public static List<Operacao>? Ordem(Historia historia, Especificacao especificacao)
    {
        ArgumentNullException.ThrowIfNull(historia);
        ArgumentNullException.ThrowIfNull(especificacao);

        var restantes = historia.Operacoes.ToList();
        var escolhidas = new List<Operacao>();

        return Andar(restantes, escolhidas, especificacao.Inicial, especificacao) ? escolhidas : null;
    }

    private static bool Andar(List<Operacao> restantes, List<Operacao> escolhidas, int estado, Especificacao especificacao)
    {
        if (restantes.Count == 0) return true;

        for (var i = 0; i < restantes.Count; i++)
        {
            var candidata = restantes[i];

            // so pode vir agora se nenhuma das que sobraram TERMINOU antes dela
            // comecar: essa e a condicao do tempo real, e e a unica diferenca
            // entre este juiz e o da consistencia sequencial
            var podeVirAgora = true;
            for (var j = 0; j < restantes.Count && podeVirAgora; j++)
                if (j != i && Historia.AconteceuAntes(restantes[j], candidata)) podeVirAgora = false;

            if (!podeVirAgora) continue;
            if (!especificacao.Aceita(estado, candidata, out var novo)) continue;

            restantes.RemoveAt(i);
            escolhidas.Add(candidata);

            if (Andar(restantes, escolhidas, novo, especificacao)) return true;

            escolhidas.RemoveAt(escolhidas.Count - 1);
            restantes.Insert(i, candidata);
        }

        return false;
    }

    /// <summary>
    /// Refaz uma ordem proposta contra a especificacao, sem usar a busca.
    ///
    /// E a conferencia que torna o juiz conferivel: se a busca devolveu uma ordem,
    /// esta funcao diz se ela presta.
    /// </summary>
    public static bool OrdemPresta(List<Operacao> ordem, Especificacao especificacao)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(especificacao);

        var estado = especificacao.Inicial;

        foreach (var operacao in ordem)
        {
            if (!especificacao.Aceita(estado, operacao, out var novo)) return false;
            estado = novo;
        }

        return true;
    }

    /// <summary>
    /// A ordem proposta respeita o TEMPO REAL: nenhuma operacao aparece depois de
    /// outra que so comecou quando a primeira ja tinha terminado.
    /// </summary>
    public static bool OrdemRespeitaORelogio(List<Operacao> ordem)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        for (var i = 0; i < ordem.Count; i++)
            for (var j = i + 1; j < ordem.Count; j++)
                if (Historia.AconteceuAntes(ordem[j], ordem[i])) return false;

        return true;
    }
}
