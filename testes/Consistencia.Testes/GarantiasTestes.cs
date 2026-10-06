using Conde.Consistencia;
using Xunit;

namespace Conde.Consistencia.Testes;

public class GarantiasTestes
{
    /// <summary>
    /// Uma leitura REGULAR devolve a ultima escrita concluida, ou alguma
    /// concorrente com ela.
    /// </summary>
    [Fact]
    public void AleituraRegularAceitaOValorNovoEOvelhoDuranteAEscrita()
    {
        var durante = new Historia(
        [
            new Operacao(0, Acao.Escrever, 1, 0, 0, 10),
            new Operacao(1, Acao.Ler, 0, 0, 2, 5),
            new Operacao(2, Acao.Ler, 0, 1, 2, 5),
        ]);

        Assert.Empty(Garantias.LeiturasNaoRegulares(durante));
    }

    /// <summary>
    /// E ela NAO aceita o valor velho depois de a escrita ter terminado: ai nao ha
    /// concorrencia nenhuma para justificar.
    /// </summary>
    [Fact]
    public void ALeituraDepoisDaEscritaConcluidaNaoPodeDevolverOVelho()
    {
        var historia = Sequencial.AQueSeparaAsDuas();
        var ruins = Garantias.LeiturasNaoRegulares(historia);

        Assert.Single(ruins);
        Assert.Equal(0, ruins[0].Resposta);
    }

    [Fact]
    public void ValorInventadoNuncaEhRegular()
    {
        var historia = Sequencial.AQueNaoEhNenhumaDasDuas();

        Assert.Single(Garantias.LeiturasNaoRegulares(historia));
        Assert.Equal(1, Garantias.LeiturasDeValorInventado(historia));
    }

    /// <summary>
    /// LEITURAS MONOTONICAS: a reclamacao mais comum de todas, que e a pagina
    /// mostrar o dado novo e depois o velho.
    /// </summary>
    [Fact]
    public void ALeituraQueAndaParaTrasEhContada()
    {
        var historia = Historia.Sequencial(
        [
            (0, Acao.Escrever, 5, 0),
            (1, Acao.Ler, 0, 5),
            (1, Acao.Ler, 0, 0),
        ]);

        Assert.Equal(1, Garantias.LeiturasQueAndaramParaTras(historia));
    }

    /// <summary>
    /// E ela e contada por PROCESSO: duas leituras de processos diferentes com
    /// valores diferentes nao sao uma violacao de leitura monotonica.
    /// </summary>
    [Fact]
    public void ProcessosDiferentesPodemVerValoresDiferentes()
    {
        var historia = Historia.Sequencial(
        [
            (0, Acao.Escrever, 5, 0),
            (1, Acao.Ler, 0, 5),
            (2, Acao.Ler, 0, 0),
        ]);

        Assert.Equal(0, Garantias.LeiturasQueAndaramParaTras(historia));
    }

    /// <summary>
    /// LEIA O QUE VOCE ESCREVEU: a garantia que o usuario cobra sem saber o nome.
    /// Para os outros, atraso e tolerado; para quem escreveu, parece perda de
    /// dado.
    /// </summary>
    [Fact]
    public void NaoVerAPropriaEscritaEhContado()
    {
        var historia = Historia.Sequencial(
        [
            (0, Acao.Escrever, 9, 0),
            (0, Acao.Ler, 0, 0),
        ]);

        Assert.Equal(1, Garantias.LeiturasQueNaoViramAPropriaEscrita(historia));

        // e ver o que OUTRO processo escreveu depois nao e violacao disso
        var outra = Historia.Sequencial(
        [
            (0, Acao.Escrever, 9, 0),
            (1, Acao.Escrever, 12, 0),
            (0, Acao.Ler, 0, 12),
        ]);

        Assert.Equal(0, Garantias.LeiturasQueNaoViramAPropriaEscrita(outra));
    }

    [Fact]
    public void AHistoriaCertaNaoViolaNada()
    {
        var historia = Historia.Sequencial(
        [
            (0, Acao.Escrever, 1, 0),
            (0, Acao.Ler, 0, 1),
            (1, Acao.Ler, 0, 1),
            (1, Acao.Escrever, 2, 0),
            (0, Acao.Ler, 0, 2),
        ]);

        var retrato = Garantias.Retratar(historia, Especificacao.Registrador());

        Assert.True(retrato.Linearizavel);
        Assert.True(retrato.SequencialmenteConsistente);
        Assert.Equal(0, retrato.LeiturasNaoRegulares);
        Assert.Equal(0, retrato.AndaramParaTras);
        Assert.Equal(0, retrato.NaoViramAPropriaEscrita);
        Assert.Equal(0, retrato.ValorInventado);
    }

    /// <summary>
    /// O ACHADO QUE DA NOME AO PROBLEMA: a historia pode ter TODAS as leituras
    /// regulares e ainda assim nao ser linearizavel, e a propriedade que quebrou
    /// e exatamente a monotonia.
    ///
    /// E por isso que as checagens baratas existem: o juiz diz que a historia nao
    /// vale, e elas dizem o que o usuario vai sentir.
    /// </summary>
    [Fact]
    public void AsChecagensBaratasDizemOQueOJuizNaoDiz()
    {
        var historia = new Historia(
        [
            new Operacao(0, Acao.Escrever, 1, 0, 0, 10),
            new Operacao(1, Acao.Ler, 0, 1, 1, 4),
            new Operacao(1, Acao.Ler, 0, 0, 5, 9),
        ]);

        Assert.False(Linearizavel.Eh(historia, Especificacao.Registrador()));
        Assert.Empty(Garantias.LeiturasNaoRegulares(historia));
        Assert.Equal(1, Garantias.LeiturasQueAndaramParaTras(historia));
    }

    /// <summary>
    /// E as checagens baratas sao mesmo BARATAS: elas rodam em historias que a
    /// busca exaustiva nao aceitaria nem tentar.
    /// </summary>
    [Fact]
    public void AsChecagensBaratasRodamEmHistoriaGrande()
    {
        var pedidos = Execucao.Sortear(6, 30, 0.4, 3);
        var historia = Execucao.Rodar(pedidos, 5, 2, 2, 3);

        Assert.True(historia.Quantas > 100);
        Assert.Throws<ArgumentException>(() => Linearizavel.Eh(historia, Especificacao.Registrador()));

        Assert.Equal(0, Garantias.LeiturasDeValorInventado(historia));
        Assert.True(Garantias.LeiturasQueAndaramParaTras(historia) >= 0);
        Assert.True(Garantias.LeiturasNaoRegulares(historia).Count >= 0);
    }
}
