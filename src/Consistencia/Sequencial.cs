namespace Conde.Consistencia;

/// <summary>
/// A CONSISTENCIA SEQUENCIAL: a mesma busca, com UMA condicao a menos.
///
/// Lamport, 1979. Uma historia e sequencialmente consistente se existe uma ordem
/// total que
///
///   1. respeita a ordem de cada PROCESSO (o que ele pediu primeiro vem primeiro),
///      e
///   2. e aceita pela especificacao sequencial.
///
/// Compare com a linearizabilidade e a diferenca e uma linha: ali a condicao e o
/// tempo real, aqui e so a ordem do programa. Nada mais muda, nem o algoritmo.
///
/// Essa linha a menos e a diferenca entre "o sistema parece uma maquina so" e "o
/// sistema parece uma maquina so, e ela e pontual". Com consistencia sequencial, o
/// seu colega pode escrever, te ligar e dizer que escreveu, e a sua leitura ainda
/// pode devolver o valor velho: nenhuma regra foi quebrada, porque a ordem do SEU
/// processo nao exige nada disso.
///
/// Toda historia linearizavel e sequencialmente consistente. A volta e falsa, e o
/// quanto ela e falsa e uma das medidas daqui.
/// </summary>
public static class Sequencial
{
    public static bool Eh(Historia historia, Especificacao especificacao, int limite = 20)
    {
        ArgumentNullException.ThrowIfNull(historia);
        ArgumentNullException.ThrowIfNull(especificacao);

        if (historia.Quantas > limite)
            throw new ArgumentException($"a busca exaustiva so cabe em ate {limite} operacoes", nameof(historia));

        return Ordem(historia, especificacao) is not null;
    }

    public static List<Operacao>? Ordem(Historia historia, Especificacao especificacao)
    {
        ArgumentNullException.ThrowIfNull(historia);
        ArgumentNullException.ThrowIfNull(especificacao);

        var processos = historia.Processos;
        var filas = new List<Operacao>[processos];
        for (var p = 0; p < processos; p++) filas[p] = historia.Do(p);

        var proximo = new int[processos];
        var escolhidas = new List<Operacao>();

        return Andar(filas, proximo, escolhidas, especificacao.Inicial, especificacao, historia.Quantas)
            ? escolhidas
            : null;
    }

    private static bool Andar(List<Operacao>[] filas, int[] proximo, List<Operacao> escolhidas, int estado, Especificacao especificacao, int faltam)
    {
        if (faltam == 0) return true;

        for (var p = 0; p < filas.Length; p++)
        {
            if (proximo[p] >= filas[p].Count) continue;

            var candidata = filas[p][proximo[p]];
            if (!especificacao.Aceita(estado, candidata, out var novo)) continue;

            proximo[p]++;
            escolhidas.Add(candidata);

            if (Andar(filas, proximo, escolhidas, novo, especificacao, faltam - 1)) return true;

            escolhidas.RemoveAt(escolhidas.Count - 1);
            proximo[p]--;
        }

        return false;
    }

    /// <summary>
    /// A ordem proposta respeita a ordem de cada PROCESSO.
    /// </summary>
    public static bool OrdemRespeitaOsProcessos(List<Operacao> ordem)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        for (var i = 0; i < ordem.Count; i++)
            for (var j = i + 1; j < ordem.Count; j++)
                if (ordem[i].Processo == ordem[j].Processo && ordem[j].Comeco < ordem[i].Comeco) return false;

        return true;
    }

    /// <summary>
    /// A historia classica que SEPARA as duas definicoes, escrita a mao.
    ///
    /// O processo 0 escreve 1 e termina. Depois disso, com o relogio ja passado, o
    /// processo 1 le e recebe 0.
    ///
    /// Nao e linearizavel: a escrita terminou antes de a leitura comecar, entao
    /// qualquer ordem valida tem que pos a escrita antes, e ai a leitura teria de
    /// devolver 1.
    ///
    /// E e sequencialmente consistente: a ordem [leitura do p1, escrita do p0]
    /// respeita a ordem de cada processo, porque cada um so tem uma operacao, e a
    /// especificacao aceita.
    ///
    /// Essa historia e o sistema que todo mundo ja usou: voce salva, avisa o
    /// colega, ele recarrega e ve o valor velho. Nenhuma regra foi quebrada.
    /// </summary>
    public static Historia AQueSeparaAsDuas() => Historia.Sequencial(
    [
        (0, Acao.Escrever, 1, 0),
        (1, Acao.Ler, 0, 0),
    ]);

    /// <summary>
    /// E a historia que nao e NENHUMA DAS DUAS: ler um valor que ninguem
    /// escreveu.
    ///
    /// Nenhuma ordem de nada explica um 7 que nunca foi escrito. E isso que separa
    /// "consistencia fraca" de "errado": consistencia fraca permite valor velho,
    /// nao permite valor inventado.
    /// </summary>
    public static Historia AQueNaoEhNenhumaDasDuas() => Historia.Sequencial(
    [
        (0, Acao.Escrever, 1, 0),
        (1, Acao.Ler, 0, 7),
    ]);
}
