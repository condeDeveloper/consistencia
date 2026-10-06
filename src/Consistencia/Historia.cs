namespace Conde.Consistencia;

/// <summary>
/// O SORTEIO, escrito aqui em vez de vir da biblioteca.
///
/// A documentacao do sorteio de biblioteca nao promete a mesma sequencia entre
/// versoes nem entre sistemas, e a integracao continua roda em tres. Uma medida
/// de "quantas historias de quorum sao linearizaveis" com interleavings
/// diferentes em cada sistema nao e uma medida: sao tres medidas que ninguem
/// pode comparar.
/// </summary>
public sealed class Sorteio
{
    private ulong estado;

    public Sorteio(ulong semente) => estado = semente == 0 ? 1 : semente;

    public uint Proximo()
    {
        estado = estado * 6364136223846793005UL + 1442695040888963407UL;
        return (uint)(estado >> 32);
    }

    public int Ate(int limite) => (int)(Proximo() % (uint)limite);

    public double Fracao() => Proximo() / 4294967296.0;

    /// <summary>Escolhe quantos itens distintos de 0 a limite-1.</summary>
    public int[] Escolher(int quantos, int limite)
    {
        var todos = Enumerable.Range(0, limite).ToArray();

        for (var i = 0; i < quantos; i++)
        {
            var j = i + Ate(limite - i);
            (todos[i], todos[j]) = (todos[j], todos[i]);
        }

        return todos[..quantos];
    }
}

/// <summary>O que o cliente pediu.</summary>
public enum Acao
{
    Ler,
    Escrever,
}

/// <summary>
/// Uma OPERACAO, com o intervalo de tempo real em que ela aconteceu.
///
/// O intervalo e a peca que separa as duas perguntas deste repositorio.
/// Consistencia sequencial so olha a ordem dentro de cada processo;
/// linearizabilidade olha tambem o relogio de parede: se uma operacao TERMINOU
/// antes de outra COMECAR, a ordem entre elas nao e negociavel.
///
/// Os tempos sao inteiros, e isso nao e simplificacao: o que importa e a ordem
/// dos eventos, e inteiro ordena sem casa decimal para discordar entre maquinas.
/// </summary>
public sealed record Operacao(int Processo, Acao Acao, int Argumento, int Resposta, int Comeco, int Fim)
{
    public override string ToString() =>
        Acao == Acao.Ler
            ? $"p{Processo} leu {Resposta} [{Comeco},{Fim}]"
            : $"p{Processo} escreveu {Argumento} [{Comeco},{Fim}]";
}

/// <summary>
/// Uma HISTORIA: o que os clientes pediram e o que receberam, com os tempos.
///
/// Ela e a unica coisa observavel de fora de um sistema distribuido. Nenhum
/// cliente ve as replicas, as versoes ou a ordem interna; ele ve o que pediu, o
/// que recebeu e quando. Toda garantia de consistencia e uma afirmacao sobre o
/// conjunto de historias que o sistema pode produzir, e e por isso que as medidas
/// daqui sao todas sobre historias.
/// </summary>
public sealed class Historia
{
    private readonly List<Operacao> operacoes;

    public Historia(IEnumerable<Operacao> operacoes)
    {
        ArgumentNullException.ThrowIfNull(operacoes);
        this.operacoes = operacoes.OrderBy(o => o.Comeco).ThenBy(o => o.Fim).ToList();
    }

    public IReadOnlyList<Operacao> Operacoes => operacoes;

    public int Quantas => operacoes.Count;

    public int Processos => operacoes.Count == 0 ? 0 : operacoes.Max(o => o.Processo) + 1;

    /// <summary>
    /// A aconteceu antes de B no relogio de parede: A terminou quando B ainda nao
    /// tinha comecado.
    ///
    /// O "menor ou igual" aqui nao e detalhe. Os intervalos sao FECHADOS no
    /// comeco e ABERTOS no fim, entao uma operacao que termina no instante 5 e
    /// outra que comeca no instante 5 nao se cruzam.
    ///
    /// Com o "menor" estrito, duas operacoes seguidas do MESMO processo apareciam
    /// como concorrentes, e a busca por linearizacao passava a aceitar ordens que
    /// invertem o programa de um cliente. O resultado foi uma coisa que nao pode
    /// existir: historias linearizaveis e nao sequencialmente consistentes.
    /// </summary>
    public static bool AconteceuAntes(Operacao a, Operacao b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return a.Fim <= b.Comeco;
    }

    /// <summary>Os intervalos se cruzam: nenhuma ordem entre elas e obrigatoria.</summary>
    public static bool Concorrentes(Operacao a, Operacao b) =>
        !AconteceuAntes(a, b) && !AconteceuAntes(b, a);

    public int QuantosParesConcorrentes()
    {
        var quantos = 0;

        for (var i = 0; i < operacoes.Count; i++)
            for (var j = i + 1; j < operacoes.Count; j++)
                if (Concorrentes(operacoes[i], operacoes[j])) quantos++;

        return quantos;
    }

    /// <summary>As operacoes de um processo, na ordem em que ele as pediu.</summary>
    public List<Operacao> Do(int processo) =>
        operacoes.Where(o => o.Processo == processo).OrderBy(o => o.Comeco).ToList();

    /// <summary>
    /// Uma historia SEQUENCIAL: cada operacao sozinha no tempo, sem concorrencia
    /// nenhuma.
    ///
    /// Ela existe para os testes: toda historia sequencial que respeita a
    /// especificacao e linearizavel por construcao, e toda historia sequencial que
    /// a viola nao e. Sem ambiguidade nenhuma, que e o jeito de conferir o juiz.
    /// </summary>
    public static Historia Sequencial(IEnumerable<(int Processo, Acao Acao, int Argumento, int Resposta)> passos)
    {
        ArgumentNullException.ThrowIfNull(passos);

        var operacoes = new List<Operacao>();
        var t = 0;

        foreach (var (processo, acao, argumento, resposta) in passos)
        {
            operacoes.Add(new Operacao(processo, acao, argumento, resposta, t, t + 1));
            t += 2;
        }

        return new Historia(operacoes);
    }

    public override string ToString() => string.Join("\n", operacoes);
}
