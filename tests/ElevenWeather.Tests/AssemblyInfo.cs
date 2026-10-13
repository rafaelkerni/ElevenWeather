// As preferências (idioma, unidade, formato de hora) são singletons globais; rodar em paralelo
// faria um teste enxergar o estado alterado por outro.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
