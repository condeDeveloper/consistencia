using Conde.Consistencia;

var pedido = args.Length > 0 ? args[0] : "tudo";
var tudo = pedido == "tudo";

if (tudo || pedido == "juiz") Juiz();
if (tudo || pedido == "quorum") Quoruns();
if (tudo || pedido == "sessao") Sessoes();
if (tudo || pedido == "reparo") Reparos();
if (tudo || pedido == "concorrencia") Concorrencias();

static void Juiz()
{
    Console.WriteLine();
    Console.WriteLine("  AS DUAS DEFINICOES, NA HISTORIA QUE AS SEPARA");
    Console.WriteLine();

    var especificacao = Especificacao.Registrador();
    var separa = Sequencial.AQueSeparaAsDuas();

    Console.WriteLine(Recuar(separa.ToString()));
    Console.WriteLine();
    Console.WriteLine($"    linearizavel:                 {(Linearizavel.Eh(separa, especificacao) ? "sim" : "nao")}");
    Console.WriteLine($"    sequencialmente consistente:  {(Sequencial.Eh(separa, especificacao) ? "sim" : "nao")}");
    Console.WriteLine();
    Console.WriteLine("  E A QUE NAO E NENHUMA DAS DUAS");
    Console.WriteLine();

    var nenhuma = Sequencial.AQueNaoEhNenhumaDasDuas();

    Console.WriteLine(Recuar(nenhuma.ToString()));
    Console.WriteLine();
    Console.WriteLine($"    linearizavel:                 {(Linearizavel.Eh(nenhuma, especificacao) ? "sim" : "nao")}");
    Console.WriteLine($"    sequencialmente consistente:  {(Sequencial.Eh(nenhuma, especificacao) ? "sim" : "nao")}");
    Console.WriteLine();

    var h = Medidas.Hierarquia();

    Console.WriteLine("  A HIERARQUIA EM HISTORIAS DE VERDADE (3 replicas, escrita 1, leitura 1)");
    Console.WriteLine();
    Console.WriteLine($"    historias:                                   {h.Historias,6:N0}");
    Console.WriteLine($"    linearizaveis:                               {h.Linearizaveis,6:N0}");
    Console.WriteLine($"    so sequencialmente consistentes:             {h.SequenciaisSoMenteS,6:N0}");
    Console.WriteLine($"    nenhuma das duas:                            {h.Nenhuma,6:N0}");
    Console.WriteLine($"    linearizaveis e nao sequenciais (impossivel): {h.LinearizavelENaoSequencial,6:N0}");
    Console.WriteLine();
}

static void Quoruns()
{
    Console.WriteLine();
    Console.WriteLine("  O QUORUM CONTRA O JUIZ (200 historias de 12 operacoes cada)");
    Console.WriteLine();
    Console.WriteLine("  N   W   R   R+W>N   linearizaveis   so sequenciais   leituras nao regulares");

    foreach (var l in Medidas.Quoruns())
        Console.WriteLine($"  {l.Replicas}   {l.Escrita}   {l.Leitura}   {(l.RegraVale ? "sim" : "nao"),5} {l.Linearizaveis,15:N0} {l.Sequenciais - l.Linearizaveis,16:N0} {l.NaoRegulares,24:N0}");

    Console.WriteLine();
}

static void Sessoes()
{
    Console.WriteLine();
    Console.WriteLine("  O QUE O USUARIO SENTE (200 historias por configuracao)");
    Console.WriteLine();
    Console.WriteLine("  N   W   R   leitura andou para tras   nao viu a propria escrita   valor inventado");

    foreach (var l in Medidas.Sessoes())
        Console.WriteLine($"  {l.Replicas}   {l.Escrita}   {l.Leitura} {l.AndaramParaTras,24:N0} {l.NaoViramAPropriaEscrita,27:N0} {l.Inventados,17:N0}");

    Console.WriteLine();
}

static void Reparos()
{
    Console.WriteLine();
    Console.WriteLine("  O REPARO DE LEITURA (3 replicas, escrita 2, leitura 2, 200 historias)");
    Console.WriteLine();
    Console.WriteLine("  jeito          linearizaveis   leitura andou para tras   leituras nao regulares");

    foreach (var l in Medidas.Reparos())
        Console.WriteLine($"  {l.Jeito,-14} {l.Linearizaveis,13:N0} {l.AndaramParaTras,25:N0} {l.NaoRegulares,24:N0}");

    Console.WriteLine();
}

static void Concorrencias()
{
    Console.WriteLine();
    Console.WriteLine("  QUANTO MAIS CONCORRENCIA, MENOS HISTORIAS LINEARIZAVEIS");
    Console.WriteLine();
    Console.WriteLine("  processos   pares concorrentes   linearizaveis");

    foreach (var l in Medidas.Concorrencias())
        Console.WriteLine($"  {l.Processos,9} {l.FracaoConcorrente,19:P1} {(double)l.Linearizaveis / l.Historias,15:P1}");

    Console.WriteLine();
}

static string Recuar(string texto) =>
    string.Join("\n", texto.Split('\n').Select(linha => "    " + linha));
