using Conde.Consistencia;
using Xunit;

namespace Conde.Consistencia.Testes;

/// <summary>
/// O juiz tambem precisa de juiz.
///
/// Toda medida deste repositorio e "quantas historias sao linearizaveis". Se a
/// busca tivesse um erro, todos os numeros pareceriam plausiveis. Por isso ela e
/// conferida de tres jeitos: contra historias sequenciais, cujo veredicto nao tem
/// ambiguidade; contra a ORDEM que ela mesma devolve, refeita sem a busca; e
/// contra a hierarquia, que diz que toda linearizavel e sequencialmente
/// consistente.
/// </summary>
public class LinearizavelTestes
{
    private static readonly Especificacao Registrador = Especificacao.Registrador();

    /// <summary>
    /// Numa historia SEQUENCIAL, linearizavel e exatamente "a especificacao
    /// aceita na ordem em que aconteceu". Nao ha nada para procurar.
    /// </summary>
    [Fact]
    public void NaHistoriaSequencialOVeredictoEhDireto()
    {
        var certa = Historia.Sequencial(
        [
            (0, Acao.Escrever, 1, 0),
            (1, Acao.Ler, 0, 1),
            (0, Acao.Escrever, 2, 0),
            (1, Acao.Ler, 0, 2),
        ]);

        var errada = Historia.Sequencial(
        [
            (0, Acao.Escrever, 1, 0),
            (1, Acao.Ler, 0, 1),
            (0, Acao.Escrever, 2, 0),
            (1, Acao.Ler, 0, 1),
        ]);

        Assert.True(Linearizavel.Eh(certa, Registrador));
        Assert.False(Linearizavel.Eh(errada, Registrador));
    }

    /// <summary>
    /// A ORDEM devolvida pela busca e refeita sem a busca: ela respeita o relogio
    /// e a especificacao aceita cada passo.
    /// </summary>
    [Fact]
    public void AOrdemDevolvidaEhConferidaSemAbusca()
    {
        var conferidas = 0;

        for (ulong semente = 1; semente <= 200; semente++)
        {
            var pedidos = Execucao.Sortear(3, 4, 0.4, semente);
            var historia = Execucao.Rodar(pedidos, 3, 2, 2, semente * 7 + 1);

            var ordem = Linearizavel.Ordem(historia, Registrador);
            if (ordem is null) continue;

            conferidas++;

            Assert.Equal(historia.Quantas, ordem.Count);
            Assert.True(Linearizavel.OrdemPresta(ordem, Registrador), $"semente {semente}");
            Assert.True(Linearizavel.OrdemRespeitaORelogio(ordem), $"semente {semente}");
        }

        Assert.True(conferidas > 150, $"so {conferidas} historias linearizaveis para conferir");
    }

    /// <summary>
    /// E a ordem devolvida tambem respeita a ordem de cada PROCESSO, de graca:
    /// dentro de um processo as operacoes nao se cruzam, entao o relogio ja
    /// obriga.
    /// </summary>
    [Fact]
    public void AOrdemDevolvidaRespeitaOProgramaDeCadaProcesso()
    {
        for (ulong semente = 1; semente <= 100; semente++)
        {
            var pedidos = Execucao.Sortear(3, 4, 0.4, semente);
            var historia = Execucao.Rodar(pedidos, 3, 2, 2, semente * 7 + 1);

            var ordem = Linearizavel.Ordem(historia, Registrador);
            if (ordem is null) continue;

            Assert.True(Sequencial.OrdemRespeitaOsProcessos(ordem), $"semente {semente}");
        }
    }

    /// <summary>
    /// O caso que a definicao existe para resolver: duas operacoes CONCORRENTES
    /// podem ser ordenadas de qualquer jeito, e basta UMA ordem funcionar.
    /// </summary>
    [Fact]
    public void BastaUmaOrdemFuncionarEntreConcorrentes()
    {
        // p0 escreve 1, p1 escreve 2, as duas ao mesmo tempo; p2 le depois
        var historia = new Historia(
        [
            new Operacao(0, Acao.Escrever, 1, 0, 0, 10),
            new Operacao(1, Acao.Escrever, 2, 0, 0, 10),
            new Operacao(2, Acao.Ler, 0, 1, 10, 12),
        ]);

        // a leitura devolveu 1, entao a ordem valida poe a escrita de 2 antes
        Assert.True(Linearizavel.Eh(historia, Registrador));

        var ordem = Linearizavel.Ordem(historia, Registrador)!;

        Assert.Equal(2, ordem[0].Argumento);
        Assert.Equal(1, ordem[1].Argumento);
    }

    /// <summary>
    /// E a mesma historia com a leitura devolvendo 2 tambem e linearizavel, pela
    /// ordem oposta. As duas sao validas, e nao ha uma certa.
    /// </summary>
    [Fact]
    public void AOrdemOpostaTambemVale()
    {
        var historia = new Historia(
        [
            new Operacao(0, Acao.Escrever, 1, 0, 0, 10),
            new Operacao(1, Acao.Escrever, 2, 0, 0, 10),
            new Operacao(2, Acao.Ler, 0, 2, 10, 12),
        ]);

        Assert.True(Linearizavel.Eh(historia, Registrador));
    }

    /// <summary>
    /// O que NAO vale: o relogio obriga. Se a escrita terminou antes de a leitura
    /// comecar, a leitura nao pode devolver o valor antigo.
    /// </summary>
    [Fact]
    public void ORelogioObrigaQuandoNaoHaConcorrencia()
    {
        var historia = Sequencial.AQueSeparaAsDuas();

        Assert.False(Linearizavel.Eh(historia, Registrador));
        Assert.Null(Linearizavel.Ordem(historia, Registrador));
    }

    /// <summary>
    /// DUAS leituras concorrentes com a mesma escrita podem devolver valores
    /// diferentes e a historia ainda ser linearizavel: a ordem poe uma antes e a
    /// outra depois.
    /// </summary>
    [Fact]
    public void DuasLeiturasConcorrentesPodemDiscordar()
    {
        var historia = new Historia(
        [
            new Operacao(0, Acao.Escrever, 1, 0, 0, 10),
            new Operacao(1, Acao.Ler, 0, 0, 1, 4),
            new Operacao(2, Acao.Ler, 0, 1, 5, 9),
        ]);

        Assert.True(Linearizavel.Eh(historia, Registrador));
    }

    /// <summary>
    /// O ACHADO QUE SEPARA REGULAR DE LINEARIZAVEL: as mesmas duas leituras na
    /// ordem INVERSA ja nao valem.
    ///
    /// A primeira leitura ve o valor novo e a segunda, que comeca depois de a
    /// primeira terminar, ve o velho. Cada leitura sozinha e defensavel (a escrita
    /// estava em andamento nas duas), e juntas elas nao sao: a ordem total teria
    /// de pos a escrita antes da primeira e depois da segunda.
    /// </summary>
    [Fact]
    public void DuasLeiturasQueAndamParaTrasNaoValem()
    {
        var historia = new Historia(
        [
            new Operacao(0, Acao.Escrever, 1, 0, 0, 10),
            new Operacao(1, Acao.Ler, 0, 1, 1, 4),
            new Operacao(2, Acao.Ler, 0, 0, 5, 9),
        ]);

        Assert.False(Linearizavel.Eh(historia, Registrador));

        // e as duas leituras, cada uma sozinha, sao regulares
        Assert.Empty(Garantias.LeiturasNaoRegulares(historia));
    }

    [Fact]
    public void HistoriaVaziaEhLinearizavel()
    {
        Assert.True(Linearizavel.Eh(new Historia([]), Registrador));
    }

    [Fact]
    public void HistoriaGrandeDemaisEhRecusada()
    {
        var pedidos = Execucao.Sortear(4, 8, 0.4, 1);
        var historia = Execucao.Rodar(pedidos, 3, 2, 2, 1);

        Assert.Throws<ArgumentException>(() => Linearizavel.Eh(historia, Registrador));
    }
}
