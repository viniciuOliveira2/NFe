using PCInfo.Plus.DataAccess;
using PCInfo.Plus.Model.Models;
using PCInfo.Utils;
using PCInfo.Utils.Enumeradores;
using PCInfo.Utils.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using PCInfo.Plus.Model.Models.Enumeradores;
using System.Xml.Linq;
using System.Xml;
using System.Text;
using PCInfo.Plus.Models.Enumeradores;
using System.Globalization;
using PCInfo.Base.ClassesUtilitarias;
using System.Net;
using System.Transactions;
using System.Text.RegularExpressions;
using DFe.Classes.Flags;
using DFe.Classes.Entidades;
using DFe.Utils;
using DFe.Classes.Assinatura;
using PCInfo.Plus.Business.Servicos;
using PCInfo.Plus.Business.AppTeste;
using PCInfo.Plus.Business.Classes.Informacoes;
using PCInfo.Plus.Business.Utils;
using PCInfo.Plus.Business.Classes.Informacoes.Detalhe;
using PCInfo.Plus.Business.Classes.Informacoes.Identificacao.Tipos;
using PCInfo.Plus.Business.Classes.Servicos.Tipos;
using PCInfo.Plus.Business.Classes.Informacoes.Emitente;
using PCInfo.Plus.Business.Classes.Informacoes.Detalhe.Tributacao;
using PCInfo.Plus.Business.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using PCInfo.Plus.Business.Classes.Informacoes.Pagamento;
using PCInfo.Plus.Business.Classes.Informacoes.Transporte;
using PCInfo.Plus.Business.Classes.Informacoes.Total;
using PCInfo.Plus.Business.Classes.Informacoes.Observacoes;
using PCInfo.Plus.Business.Classes.Informacoes.Cobranca;
using PCInfo.Plus.Business.Classes.Informacoes.Destinatario;
using PCInfo.Plus.Business.Classes.Informacoes.Identificacao;
using PCInfo.Plus.Business.Classes.Informacoes.Detalhe.Tributacao.Estadual.Tipos;
using PCInfo.Plus.Business.Classes.Informacoes.Detalhe.Tributacao.Federal;
using PCInfo.Plus.Business.Classes.Informacoes.Detalhe.Tributacao.Federal.Tipos;
using PCInfo.Plus.Business.Utils.Tributacao.Estadual;
using PCInfo.Plus.Business.Utils.Tributacao.Federal;
using PCInfo.Plus.Business.Utils.Validacao;
using PCInfo.Plus.Business.Utils.Assinatura;
using PCInfo.Plus.Business.Classes.Informacoes.Detalhe.ProdEspecifico;
using PCInfo.Plus.Business.Classes;
using PCInfo.Plus.Business.Classes.Soap.ConsultaNotasProprias;
using PCInfo.Plus.Business.Classes.Soap;
using System.IO.Compression;
using DFe.Utils.Assinatura;
using PCInfo.Plus.Business.Classes.Soap.Enumeradores;
using PCInfo.Plus.Business.Classes.Soap.Servicos;
using PCInfo.Plus.Business.Classes.Soap.Manifestação;
using PCInfo.Plus.Business.Classes.Soap.Comunicacao;
using System.Threading.Tasks;
using PCInfo.Plus.Business.Utils.NFe;
using PCInfo.Plus.Business.Servicos.Retorno;
using PCInfo.Plus.Model;
using Microsoft.VisualBasic.Devices;
using PCInfo.Plus.Business.Classes.Soap.Shared.NFe.Classes.Informacoes.Observacoes;

namespace PCInfo.Plus.Business
{
   public class NFeBusiness : GenericoBusiness<tb_nfe>
   {
      private tb_parametro parametro;
      private tb_nfe nfe;
      private tb_empresa empresa;
      private TipoAmbiente ambiente = TipoAmbiente.Homologacao;
      private const string NUMERO_INICIAL_NSU = "000000000000000";
      public delegate void ErroNFHandler(Exception ex, string xml);
      private tb_nf_configuracao configuracaoNF;
      public event EventoEnviarMensagem EnviarMensagem;
      public delegate void EventoEnviarMensagem(string mensagem);
      decimal valorAcumulado = 0;
      X509Certificate2 certificadoDigital = null;

      #region Soap
      private ConfiguracaoApp _configuracoes;
      private ChaveFiscal _ChaveFiscal;
      private const string ArquivoConfiguracao = @"\configuracao.xml";
      det detalhe4_0 = new det();
      private Classes.NFe _nfe;
      #endregion

      #region GerarNFe 4.0
      public void GerarNFe4_0(tb_nfe nf, bool enviarNFe, bool salvarApenas = false)
      {
         parametro = new ParametroBusiness().BuscarParametroVigente();
         nfe = nf;
         ambiente = (TipoAmbiente)nf.Ambiente;
         empresa = new EmpresaBusiness().BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);

         configuracaoNF = parametro.tb_nf_configuracao;

         if (!string.IsNullOrEmpty(parametro.CertificadoDigital) || parametro.CertificadoDigital == "")
         {
            certificadoDigital = new Certificado().Carregar(parametro.CertificadoDigital);
            if (certificadoDigital != null && enviarNFe)
               ValidarValidadeCertificado(certificadoDigital);
         }
         else if (EnviarMensagem != null && enviarNFe)
            throw new BusinessException("O Certificado Digital não foi configurado.");

         if (string.IsNullOrEmpty(configuracaoNF.CaminhoArquivo) || !new DirectoryInfo(configuracaoNF.CaminhoArquivo).Exists)
            throw new BusinessException("Não é possível salvar o XML da NF-e, pois o caminho informado no parâmetro não existe, ou não é válido. Para configurar o mesmo acesse o menu Controle, Parâmetros, aba NF-e e informe um caminho válido.");

         var nomePasta = (nf.DataEmissao.Year + "-" + nf.DataEmissao.Month.ToString().PadLeft(2, '0')).ToString();
         if (!Directory.Exists(configuracaoNF.CaminhoArquivo + "\\" + nomePasta))
            try { Directory.CreateDirectory(configuracaoNF.CaminhoArquivo + "\\" + nomePasta); }
            catch (Exception ex)
            { throw new BusinessException("Não foi possível gerar o XML da NFe pois o caminho configurado no parâmetro não existe ou acesso ao caminho foi negado"); }

         ValidarCampos(nf);

         _configuracoes = new ConfiguracaoApp();
         _configuracoes.CfgServico.TimeOut = 6000;
         _configuracoes.CfgServico.cUF = (Estado)CodigoEstado(empresa);
         _configuracoes.CfgServico.ModeloDocumento = ModeloDocumento.NFe;
         _configuracoes.CfgServico.Certificado.Serial = certificadoDigital.SerialNumber;
         _configuracoes.CfgServico.tpEmis = (TipoEmissao)nfe.TipoEmissao;
         _configuracoes.CfgServico.Certificado.ManterDadosEmCache = true;
         _configuracoes.CfgServico.SalvarXmlServicos = true;
         _configuracoes.CfgServico.DiretorioSalvarXml = configuracaoNF.CaminhoArquivo + "\\" + nomePasta;
         if (nf.TipoEmissao == (int)TipoEmissao.teSVCRS)
            _configuracoes.CfgServico.VersaoNFeAutorizacao = VersaoServico.ve310;
         else
            _configuracoes.CfgServico.VersaoNFeAutorizacao = VersaoServico.ve400;
         _configuracoes.CfgServico.VersaoNfeStatusServico = VersaoServico.ve400;
         _configuracoes.CfgServico.VersaoRecepcaoEventoEpec = VersaoServico.ve400;
         _configuracoes.CfgServico.VersaoNFeRetAutorizacao = VersaoServico.ve400;

         var DiretorioRaiz = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

         _configuracoes.CfgServico.DiretorioSchemas = DiretorioRaiz + "\\PCInfo\\Schemas";
         _configuracoes.CfgServico.ProtocoloDeSeguranca = SecurityProtocolType.Tls12;
         if (ambiente == TipoAmbiente.Producao)
            _configuracoes.CfgServico.tpAmb = TipoAmbiente.Producao;
         else
            _configuracoes.CfgServico.tpAmb = TipoAmbiente.Homologacao;

         var numero = nf.NumeroNota;
         if (enviarNFe && nf.TipoEmissao != (int)TipoEmissao.teFSDA && nf.TipoEmissao != (int)TipoEmissao.teFSIA)//emissão NFe normal
         {
            if (certificadoDigital == null)
            {
               if (EnviarMensagem != null) EnviarMensagem("O Certificado Digital não foi configurado.");
               return;
            }

            var numeroLote = nf.id;

            _nfe = ObterNfeValidada(_configuracoes.CfgServico.VersaoNFeAutorizacao = VersaoServico.ve400, _configuracoes.CfgServico.ModeloDocumento, Convert.ToInt32(numero), _configuracoes.ConfiguracaoCsc, _configuracoes.CfgServico);
            ServicosNFe servicosNfe = new ServicosNFe(_configuracoes.CfgServico);

            if (nf.idNFStatus == (int)EnumStatusNFe.EpecAguardandoEnvio)
               nfe.TipoEmissao = (int)TipoEmissao.teNormal;//muda o status para reemitir a Nfe normalmente.

            if (nf.TipoEmissao == (int)TipoEmissao.teEPEC && nf.idNFStatus != (int)EnumStatusNFe.EpecAguardandoEnvio)
               EnviarEpec(servicosNfe, numeroLote, 1, _nfe, PCInfo.Base.Core.Principal.Versao.ToString(), nf);
            else
               EnviarNFe4_0(servicosNfe, new List<Classes.NFe> { _nfe }, numeroLote.ToString(), nf, certificadoDigital);
         }
         else if (enviarNFe && nf.TipoEmissao.In((int)TipoEmissao.teFSDA, (int)TipoEmissao.teFSIA))//formulario de segurança
         {
            _nfe = ObterNfeValidada(_configuracoes.CfgServico.VersaoNFeAutorizacao, _configuracoes.CfgServico.ModeloDocumento, Convert.ToInt32(numero), _configuracoes.ConfiguracaoCsc, _configuracoes.CfgServico);
            string xml = ObterXmlString(_nfe);
            AtualizarNFe4_0(nfe, _nfe.infNFe, xml);
         }
         else if (!enviarNFe && salvarApenas)//salvar nota apenas para gerar danfe. 
         {
            _nfe = ObterNfeValidada(_configuracoes.CfgServico.VersaoNFeAutorizacao = VersaoServico.ve400, _configuracoes.CfgServico.ModeloDocumento, Convert.ToInt32(numero), _configuracoes.ConfiguracaoCsc, _configuracoes.CfgServico);
            string xml = ObterXmlString(_nfe);
            GerarChave4_0(_nfe.infNFe, nfe, xml);
         }
      }

      private Classes.NFe ObterNfeValidada(VersaoServico versaoServico, ModeloDocumento modelo, int numero, ConfiguracaoCsc configuracaoCsc, ConfiguracaoServico cfgServico = null)
      {
         var nfe = GetNf(numero, modelo, versaoServico);

         Assina(nfe, cfgServico, certificadoDigital);

         Valida(nfe);

         return nfe;
      }
      protected virtual Classes.NFe GetNf(int numero, ModeloDocumento modelo, VersaoServico versao)
      {
         var nf = new Classes.NFe { infNFe = GetInf(numero, modelo, versao) };
         return nf;
      }
      protected virtual infNFe GetInf(int numero, ModeloDocumento modelo, VersaoServico versao)
      {
         var infNFe = new infNFe
         {
            versao = versao.VersaoServicoParaString(),
            ide = GetIdentificacao(numero, modelo, versao),
            emit = GetEmitente(),
            dest = GetDestinatario(versao, modelo),
            transp = GetTransporte()
         };
         int numeroItem = 0;
         foreach (tb_nfe_produto item in nfe.tb_nfe_produto)
         {
            numeroItem++;
            infNFe.det.Add(GetDetalhe(numeroItem, item, infNFe.emit.CRT, modelo));
         }

         infNFe.total = GetTotal(versao, infNFe.det, nfe.tb_nfe_produto);

         //parcelas de pagamento
         if (infNFe.ide.mod == ModeloDocumento.NFe & (versao == VersaoServico.ve310 || versao == VersaoServico.ve400))
            if (nfe.tb_nfe_pagamento != null)
            {
               infNFe.cobr = GetCobranca(nfe); //V3.00 e 4.00 Somente                       
            }

         //formas de pagamento
         if (infNFe.ide.mod == ModeloDocumento.NFCe || (infNFe.ide.mod == ModeloDocumento.NFe & versao == VersaoServico.ve400))
         {
            nfe.tb_nfe_formapagamento = new NFeFormaPagamentoBusiness().BuscarPorIdNota(nfe.id);
            if (nfe.tb_nfe_formapagamento != null)
            {
               pag pag = new pag();
               infNFe.pag = new List<pag>();
               pag = GetPagamento(nfe);
               infNFe.pag.Add(pag);
            }
         }

         //informações adicinais
         if (infNFe.ide.mod == ModeloDocumento.NFCe & versao != VersaoServico.ve400)
            infNFe.infAdic = new infAdic() { infCpl = "Troco: 10,00" }; //Susgestão para impressão do troco em NFCe

         if (nfe.tb_nfe_produto.Sum(x => x.ValorCreditoICMS).GetValueOrDefault() > 0 || nfe.tb_nfe_produto.Sum(x => x.ValorICMS).GetValueOrDefault() > 0)
         {
            var valorCreditoICMS = nfe.tb_nfe_produto.Sum(x => x.ValorCreditoICMS);
            if (valorCreditoICMS.HasValue)
            {
               var informacoesComplementaresFisco = nfe.InformacoesFisco;

               if (!string.IsNullOrEmpty(nfe.InformacoesComplementares) || !string.IsNullOrEmpty(informacoesComplementaresFisco))
               {
                  infNFe.infAdic = new infAdic()
                  {
                     infAdFisco = informacoesComplementaresFisco != string.Empty ? (StringUtis.SubstituirCarecteresAcentuados(informacoesComplementaresFisco).Trim()) : null,
                     infCpl = nfe.InformacoesEditaveis != string.Empty ? nfe.InformacoesEditaveis.Trim().Replace("\r\n"," ") + " /" + nfe.InformacoesComplementares.Trim() : nfe.InformacoesComplementares.Trim()
                  };
               }
            }
         }
         else if (!string.IsNullOrEmpty(nfe.InformacoesComplementares))
         {
            infNFe.infAdic = new infAdic()
            {
               infCpl = nfe.InformacoesEditaveis != string.Empty ? nfe.InformacoesComplementares.Trim() + " /" + nfe.InformacoesEditaveis.Trim().Replace("\r\n", " ") : nfe.InformacoesComplementares.Trim()
            };

         }
         else if(!string.IsNullOrEmpty(nfe.InformacoesEditaveis))
         {
            infNFe.infAdic = new infAdic()
            {
               infCpl = nfe.InformacoesEditaveis.Trim().Replace("\r\n", " ")
            };
         }

         //tag nota para exterior
         if (nfe.TipoNota == EnumTipoNota.Saida && infNFe.ide.idDest == DestinoOperacao.doExterior)
         {
            infNFe.exporta = new exporta
            {
               UFSaidaPais = (PCInfo.Base.Core.Principal.Empresa as tb_empresa).tb_cidade.tb_estado.Sigla,
               xLocExporta = "BRASIL",
               xLocDespacho = (PCInfo.Base.Core.Principal.Empresa as tb_empresa).Endereco,
            };
         }
         
         //dados padrão do responsável tecnico
         infNFe.infRespTec = new infRespTec()
         {
            CNPJ = "17699757000118",
            xContato = "Vinicius Vieira de Moraes",
            email = "vinicius@grupopcsolucoes.com.br",
            fone = "3432398901"
         };

         //tag autorizado a fazer donwload  de xml
         var contador = new ContadorBusiness().BuscarPorId(empresa.idContador.GetValueOrDefault());
         if (contador != null)
            infNFe.autXML = GetAutXML(empresa, contador);

         return infNFe;
      }

      protected virtual List<autXML> GetAutXML(tb_empresa empresa, tb_contador contador)
      {
         var inscricao = "";
         var CPF = "";

         if (contador != null)
         {
            inscricao = contador.CNPJJuridica.Trim().Replace(".", "").Replace("-", "").Replace("/", "");
            if(inscricao==string.Empty || inscricao == null)
            CPF = contador.CPF.Trim().Replace(".", "").Replace("-", "");
         }

         var aut = new List<autXML>();

         var autx = new autXML
         {
            CNPJ = inscricao != null ? inscricao : null,
            CPF = CPF != null ? CPF : null,
         };
         aut.Add(autx);
         return aut;
      }
      protected virtual ide GetIdentificacao(int numero, ModeloDocumento modelo, VersaoServico versao)
      {
         var codNF = "";
         if (!string.IsNullOrEmpty(nfe.Chave))
            codNF = nfe.Chave.Substring(35, 8);
         else
            codNF = new Random().Next(99999999).ToString().PadLeft(8, '0');
         var naturezaOperacao = nfe.NaturezaOperacao.Length > 50 ? nfe.NaturezaOperacao.Substring(0, 50).ToUpper().Trim() : nfe.NaturezaOperacao.ToUpper().Trim();
         RemoverAcentos(naturezaOperacao);
         var NfeReferencia = new NFeReferenciaBusiness().BuscarPorIdNota(nfe.id);
         var nfeReferenciaContranota = new NFeReferenciaContranotaBusiness().BuscarPorIdNota(nfe.id);

         var ide = new ide
         {
            cUF = (Estado)CodigoEstado(empresa),
            natOp = naturezaOperacao,
            mod = modelo,
            serie = configuracaoNF.NumeroSerie.Value,
            nNF = numero,
            tpNF = nfe.TipoNota == EnumTipoNota.Entrada && nfe.TipoNota == EnumTipoNota.DevolucaoVenda ? TipoNFe.tnEntrada : TipoNFe.tnSaida,
            cMunFG = empresa.tb_cidade.CodigoIBGE.Value,
            tpEmis = _configuracoes.CfgServico.tpEmis,
            tpImp = TipoImpressao.tiRetrato,
            cNF = codNF,
            tpAmb = ambiente,
            finNFe = (FinalidadeNFe)nfe.CodigoFinalidade,
            verProc = PCInfo.Base.Core.Principal.Versao.ToString(),
         };
         //devolução NF-e
         if (NfeReferencia.Count > 0)
            ide.NFrefe = new List<NFrefe>();
         foreach (var item in NfeReferencia)
         {
            var refeNfe = new NFrefe
            {
               refNFe = item.Chave,
            };
            ide.NFrefe.Add(refeNfe);
         }
         //devolução contra nota
         if (nfeReferenciaContranota.Count > 0)
         {
            ide.NFrefe = new List<NFrefe>();
            var clienteContranota = new ClienteBusiness().BuscarPorId(nfe.idCliente);
            foreach (var item in nfeReferenciaContranota)
            {
               var refe = new NFrefe()
               {
                  refNFP = new refNFP
                  {
                     cUF = (Estado)CodigoEstadoCliente(clienteContranota),
                     AAMM = item.DataEmissao.DateTimeOrNullToString("yyMM"),
                     CNPJ = clienteContranota.TipoInscricao == (int)EnumTipoInscricao.CNPJ ? clienteContranota.NumeroInscricao : null,
                     CPF = clienteContranota.TipoInscricao == (int)EnumTipoInscricao.CPF ? clienteContranota.NumeroInscricao : null,
                     IE = clienteContranota.InscricaoEstadual,
                     mod = item.idEspecie.ToString().PadLeft(2, '0'),
                     serie = item.Serie.ToInt32(),
                     nNF = item.NumeroNota.ToInt32()

                  }
               };
               ide.NFrefe.Add(refe);
            }
         }

         //devolução de cupom fiscal
         var NfeReferenciaCupom = new NFeReferenciaCupomBusiness().BuscarPorIdNota(nfe.id);
         if (NfeReferenciaCupom.Count > 0)
            ide.NFrefe = new List<NFrefe>();
         foreach (var item in NfeReferenciaCupom)
         {
            var refe = new NFrefe()
            {
               refECF = new refECF
               {
                  mod = item.Modelo,
                  nECF = item.NumeroECF.ToInt32(),
                  nCOO = item.NumeroCupom.ToInt32()
               }
            };

            ide.NFrefe.Add(refe);
         }

         if (ide.tpEmis != TipoEmissao.teNormal)
         {
            ide.dhCont = DateTime.Now;
            ide.xJust = "TESTE DE CONTIGÊNCIA PARA NFe/NFCe";
         }

         #region V2.00

         if (versao == VersaoServico.ve200)
         {
            ide.dEmi = DateTime.Today; //Mude aqui para enviar a nfe vinculada ao EPEC, V2.00
            ide.dSaiEnt = DateTime.Today;
         }

         #endregion

         #region V3.00

         if (versao == VersaoServico.ve200) return ide;

         if (versao == VersaoServico.ve310)
         {
            ide.indPag = IndicadorPagamento.ipVista;
         }

         var cliente = new ClienteBusiness().BuscarPorId(nfe.idCliente);
         if (empresa.tb_cidade.idEstado == cliente.tb_cidade.idEstado)
            ide.idDest = DestinoOperacao.doInterna;
         else if (empresa.tb_cidade.idEstado != cliente.tb_cidade.tb_estado.id && empresa.tb_cidade.tb_estado.idPais == cliente.tb_cidade.tb_estado.idPais)
            ide.idDest = DestinoOperacao.doInterestadual;
         else
            ide.idDest = DestinoOperacao.doExterior;
         ide.dhEmi = nfe.DataEmissao;
         //Mude aqui para enviar a nfe vinculada ao EPEC, V3.10
         if (ide.mod == ModeloDocumento.NFe)
            ide.dhSaiEnt = (DateTime)nfe.DataEntradaSaida;
         else
            ide.tpImp = TipoImpressao.tiNFCe;

         if (nfe.TipoNota == EnumTipoNota.Entrada)
            ide.tpNF = TipoNFe.tnEntrada;
         else if (nfe.TipoNota == EnumTipoNota.Saida)
            ide.tpNF = TipoNFe.tnSaida;
         else if (nfe.TipoNota == EnumTipoNota.DevolucaoVenda)
            ide.tpNF = TipoNFe.tnEntrada;
         else if (nfe.TipoNota == EnumTipoNota.DevolucaoCompra)
            ide.tpNF = TipoNFe.tnSaida;

         ide.procEmi = ProcessoEmissao.peAplicativoContribuinte;
         ide.indFinal = ConsumidorFinal.cfConsumidorFinal; //NFCe: Tem que ser consumidor Final
         ide.indPres = PresencaComprador.pcPresencial; //NFCe: deve ser 1 ou 4

         #endregion
         return ide;
      }
      private int CodigoEstadoCliente(tb_cliente cliente)
      {
         string Sigla = cliente.tb_cidade.tb_estado.Sigla;
         int codEstado = 0;
         if (Sigla == "RO")
            codEstado = 11;
         else if (Sigla == "AC")
            codEstado = 12;
         else if (Sigla == "AM")
            codEstado = 13;
         else if (Sigla == "RR")
            codEstado = 14;
         else if (Sigla == "PA")
            codEstado = 15;
         else if (Sigla == "AP")
            codEstado = 16;
         else if (Sigla == "TO")
            codEstado = 17;
         else if (Sigla == "MA")
            codEstado = 21;
         else if (Sigla == "PI")
            codEstado = 22;
         else if (Sigla == "CE")
            codEstado = 23;
         else if (Sigla == "RN")
            codEstado = 24;
         else if (Sigla == "PB")
            codEstado = 25;
         else if (Sigla == "PE")
            codEstado = 26;
         else if (Sigla == "AL")
            codEstado = 26;
         else if (Sigla == "SE")
            codEstado = 28;
         else if (Sigla == "BA")
            codEstado = 29;
         else if (Sigla == "MG")
            codEstado = 31;
         else if (Sigla == "ES")
            codEstado = 32;
         else if (Sigla == "RJ")
            codEstado = 33;
         else if (Sigla == "SP")
            codEstado = 35;
         else if (Sigla == "PR")
            codEstado = 41;
         else if (Sigla == "SC")
            codEstado = 42;
         else if (Sigla == "RS")
            codEstado = 43;
         else if (Sigla == "MS")
            codEstado = 50;
         else if (Sigla == "MT")
            codEstado = 51;
         else if (Sigla == "GO")
            codEstado = 52;
         else if (Sigla == "DF")
            codEstado = 53;
         else if (Sigla == "EX")
            codEstado = 99;
         return codEstado;
      }
      protected virtual List<refECF> GetRefDevolucaoCupom(tb_nfe nf)
      {
         List<refECF> rf = new List<refECF>();
         var NfeReferencia = new NFeReferenciaCupomBusiness().BuscarPorIdNota(nfe.id);
         foreach (var item in NfeReferencia)
         {
            var r = new refECF
            {
               mod = item.Modelo,
               nECF = item.NumeroECF.ToInt32(),
               nCOO = item.NumeroCupom.ToInt32()
            };
            rf.Add(r);
         }
         return rf;
      }
      protected virtual emit GetEmitente()
      {
         var emit = _configuracoes.Emitente;
         emit = new emit
         {
            //CPF = "80365027553",
            CNPJ = empresa.NumeroInscricao,
            xNome = empresa.RazaoSocial.Length >50? throw new BusinessException("Razão Social do emitente não pode conter mais de 50 caracteres"): RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(empresa.RazaoSocial)).Trim(),
            xFant = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(empresa.NomeFantasia)).Trim(),
            IE = RemoveCaracteresEspeciaisNFe(empresa.InscricaoEstadual.Trim()),
            CRT = parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional ? CRT.SimplesNacional : CRT.RegimeNormal
         };
         emit.enderEmit = GetEnderecoEmitente();
         return emit;
      }
      protected virtual dest GetDestinatario(VersaoServico versao, ModeloDocumento modelo)
      {
         var cliente = new ClienteBusiness().BuscarPorId(nfe.idCliente);
         string CNPJ = "";
         string CPF = "";
         if (cliente.TipoInscricao == (int)EnumTipoInscricao.CNPJ)
         {
            if (cliente.NumeroInscricao.Length == 14)
               CNPJ = cliente.NumeroInscricao.Trim();
            else
               throw new BusinessException("CNPJ do Cliente Inválido!");
         }
         else if (cliente.TipoInscricao == (int)EnumTipoInscricao.CPF)
         {
            if (cliente.NumeroInscricao.Length == 11)
               CPF = cliente.NumeroInscricao.Trim();
            else
               throw new BusinessException("CPF do Cliente Inválido!");
         }
         var dest = new dest(versao)
         {
            CNPJ = CNPJ,
            CPF = CPF,
            IE = cliente.InscricaoEstadual.Trim()                                                     
         };
         if (empresa.tb_cidade.tb_estado.idPais != cliente.tb_cidade.tb_estado.idPais)
            dest.idEstrangeiro = "";

         if (modelo == ModeloDocumento.NFe)
         {
            if (ambiente == TipoAmbiente.Homologacao)
               dest.xNome = "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";
            else
               dest.xNome = cliente.RazaoSocial.Length > 50 ? throw new BusinessException("Razão Social do Destinatário não pode conter mais de 50 caracteres!"):RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(cliente.RazaoSocial)).Trim();

            
            dest.enderDest = GetEnderecoDestinatario(cliente); //Obrigatório para NFe e opcional para NFCe
         }
         if (versao == VersaoServico.ve200) return dest;

         dest.indIEDest = (indIEDest)cliente.IndicadorIEDestinatario;
         dest.email = !string.IsNullOrEmpty(cliente.Email) ? cliente.Email : null;
         return dest;
      }
      protected virtual enderDest GetEnderecoDestinatario(tb_cliente cliente)
      {
         var cidade = new CidadeBusiness().BuscarPorId(cliente.idCidade);
         var enderDest = new enderDest
         {
            xLgr = RemoverAcentos(cliente.Endereco).TrimStart(),
            nro = !string.IsNullOrEmpty(cliente.NumeroEndereco) ? RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(cliente.NumeroEndereco.ToString())).Trim() : "S/N",
            xBairro = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(cliente.Bairro)).Trim(),
            cMun = cidade.tb_estado.tb_pais.id != 30 ? 9999999 : cidade.CodigoIBGE.GetValueOrDefault(),
            xMun = StringUtis.SubstituirCarecteresAcentuados(cidade.Nome.Trim()),
            UF = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(cidade.tb_estado.Sigla)),
            CEP = !string.IsNullOrEmpty(cliente.CEP) ? cliente.CEP.ToStringOrNull().Trim() : string.Empty,
            cPais = cidade.tb_estado.tb_pais.CodigoBC.Value,
            xPais = StringUtis.SubstituirCarecteresAcentuados(cidade.tb_estado.tb_pais.Nome.ToUpper().Trim())
         };
         return enderDest;
      }
      protected virtual enderEmit GetEnderecoEmitente()
      {
         var dddc = empresa.DDD.Value.ToString().Trim();

         var enderEmit = _configuracoes.EnderecoEmitente;
         enderEmit = new enderEmit
         {
            xLgr = (StringUtis.SubstituirCarecteresAcentuados(empresa.Endereco)),
            nro = empresa.NumeroEndereco.ToString(),
            xCpl = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(empresa.Complemento)).Trim(),
            xBairro = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(empresa.Bairro)).Trim(),
            cMun = empresa.tb_cidade.CodigoIBGE.Value,
            xMun = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados((empresa.tb_cidade.Nome)).Trim()),
            UF = empresa.tb_cidade.tb_estado.Sigla,
            CEP = empresa.Cep.Trim(),
            fone = (dddc.Trim() + empresa.Telefone.Trim()).ToLong()
         };
         enderEmit.cPais = 1058;
         enderEmit.xPais = "BRASIL";
         return enderEmit;
      }
      protected virtual transp GetTransporte()
      {
         var nfFrete = nfe.tb_nfe_frete;
         var volumes = new List<vol> { GetVolume(nfFrete) };
         nfFrete.tb_transportadora = new TransportadoraBusiness().BuscarPorId(nfFrete.idTransportadora.GetValueOrDefault());
         var transportadoraVeiculo = new TransportadoraVeiculoBusiness().BuscarPorIdVeiculo(nfFrete.idVeiculo.GetValueOrDefault());
         var t = new transp
         {
            modFrete = (ModalidadeFrete)nfFrete.idTipoFrete, //NFCe: Não pode ter frete
            transporta = nfFrete.tb_transportadora != null ? GetTransporta(nfFrete) : null,
            vol = nfFrete.idTipoFrete != (int)ModalidadeFrete.ProprioContaDestinatario ? null : volumes,
            veicTransp = transportadoraVeiculo != null ? GetVeiculo(transportadoraVeiculo) : null
         };

         return t;
      }
      protected virtual veicTransp GetVeiculo(tb_transportadora_veiculo veiculo)
      {
         var v = new veicTransp
         {
            placa = veiculo.Placa.Replace("-", ""),
            UF = veiculo.tb_estado.Sigla
         };

         return v;
      }
      protected virtual vol GetVolume(tb_nfe_frete nfFrete)
      {
         var v = new vol
         {
            qVol = nfFrete.QuantidadeVolumes.ToInt32(),
            esp = !string.IsNullOrEmpty(nfFrete.EspecieVolumes) ? RemoveCaracteresEspeciaisNFe(nfFrete.EspecieVolumes) : string.Empty,
            pesoL = Math.Round(nfFrete.PesoLiquido.Value, 3),
            pesoB = Math.Round(nfFrete.PesoBruto.Value, 3),
            marca = !string.IsNullOrEmpty(nfFrete.Marca) ? RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(nfFrete.Marca)).Trim() : null,
            nVol = !string.IsNullOrEmpty(nfFrete.Numeracao) ? RemoveCaracteresEspeciaisNFe(nfFrete.Numeracao) : null,
            //lacres = new List<lacres> { new lacres { nLacre = "123456" } }
         };

         return v;
      }

      protected virtual transporta GetTransporta(tb_nfe_frete nfFrete)
      {
         var t = new transporta
         {
            xNome = nfFrete.tb_transportadora.NomeTransportadora.Trim(),
            CNPJ = nfFrete.tb_transportadora.TipoInscricao == Constants.CNPJ ? RemoveCaracteresEspeciaisNFe(nfFrete.tb_transportadora.NumeroInscricao.Trim()).Trim() : null,
            CPF = nfFrete.tb_transportadora.TipoInscricao == Constants.CPF ? nfFrete.tb_transportadora.NumeroInscricao.Trim() : null,
            xEnder = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(nfFrete.tb_transportadora.EnderecoCompleto)).Trim(),
            xMun = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(nfFrete.tb_transportadora.Cidade)).Trim(),
            UF = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(nfFrete.tb_transportadora.UF)).Trim(),
            IE = nfFrete.tb_transportadora.IndicadorIEDestinatario == (short)indIEDest.ContribuinteICMS ?
           RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(nfFrete.tb_transportadora.InscricaoEstadual)).Trim().Replace(" ", "").Trim() :
           "ISENTO"
         };
         return t;
      }
      protected virtual veicTransp GetVeiculoTrasporte(tb_nfe_frete nfFrete)
      {
         var transportadoraVeiculo = new TransportadoraVeiculoBusiness().BuscarPorIdVeiculo(nfFrete.idVeiculo.GetValueOrDefault());
         var v = new veicTransp
         {
            placa = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados((transportadoraVeiculo.Placa.Replace("-", "").TrimStart()))).Trim(),
            UF = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(transportadoraVeiculo.tb_estado.Sigla)).Trim()

         };
         return v;
      }
      protected virtual cobr GetCobranca(tb_nfe nf)
      {
         var c = new cobr();
         {
            c.fat = new fat { nFat = nf.NumeroNota.ToString(), vLiq = Math.Round(nf.tb_nfe_pagamento.Sum(x => x.Valor), 2), vOrig = Math.Round(nf.tb_nfe_pagamento.Sum(x => x.Valor), 2), vDesc = 0m };
            c.dup = GetDuplicata(nf);
         }
         return c;
      }
      protected virtual List<dup> GetDuplicata(tb_nfe nf)
      {
         List<dup> du = new List<dup>();
         foreach (var itemPag in nfe.tb_nfe_pagamento)
         {
            var d = new dup
            {
               nDup = itemPag.Numero.ToString().PadLeft(3, '0'),
               dVenc = itemPag.DataVencimento,
               vDup = itemPag.Valor
            };
            du.Add(d);
         }
         return du;
      }
      protected virtual pag GetPagamento(tb_nfe nf)
      {
         pag p = new pag
         {
            detPag = GetDetPagamento(nf)
         };
         return p;
      }
      protected virtual List<detPag> GetDetPagamento(tb_nfe nf)
      {
         List<detPag> dp = new List<detPag>();
         
         //condição para quando for nota de devolução e do estado Mato Grosso do Sul informar tag Vpag zerada.
         bool Valor =false;
         if (nfe.CodigoFinalidade == (int)FinalidadeNFe.fnDevolucao && empresa.UF == "MS")
            Valor = true;
         //
         foreach (var item in nf.tb_nfe_formapagamento)
         {
            var d = new detPag { tPag = (FormaPagamento)item.FormaDePagamento.ToInt32(),
                                 vPag = Valor==false? item.ValorDoPagamento.GetValueOrDefault(): 0};
            dp.Add(d);
         }
         return dp;
      }
      protected virtual det GetDetalhe(int i, tb_nfe_produto item, CRT crt, ModeloDocumento modelo)
      {
         var det = new det
         {
            nItem = i,
            prod = GetProduto(item),
            imposto = new imposto
            {
               vTotTrib = 0,

               ICMS = new ICMS
               {
                  //Se você já tem os dados de toda a tributação persistida no banco em uma única tabela, utilize a linha comentada abaixo para preencher as tags do ICMS
                  //TipoICMS = ObterIcmsBasico(crt),

                  //Caso você resolva utilizar método ObterIcmsBasico(), comente esta proxima linha
                  TipoICMS =
                         crt == CRT.SimplesNacional
                             ? InformarCSOSN((Csosnicms)item.idCSOSN, item)
                             : InformarICMS((Csticms)item.idCSTICMS, item)
               },
            }
         };

         //informar pis na NFe
         if (item.idCSTPIS <= 2)
            det.imposto.PIS = new PIS
            {
               TipoPIS = new PISAliq
               {
                  CST = (CSTPIS)item.idCSTPIS.GetValueOrDefault(),
                  pPIS = item.ValorAliquotaPIS.GetValueOrDefault(),
                  vBC = item.ValorBaseCalculoPIS.GetValueOrDefault(),
                  vPIS = Math.Round(ValorUtils.TruncateDecimal(item.ValorPIS.GetValueOrDefault(), 4), 2)
               }
            };
         else if (item.idCSTPIS == 3)
            det.imposto.PIS = new PIS
            {
               TipoPIS = new PISQtde
               {
                  CST = (CSTPIS)item.idCSTPIS.GetValueOrDefault(),
                  qBCProd = item.Quantidade,
                  vAliqProd = item.ValorAliquotaPIS.GetValueOrDefault(),
                  vPIS = Math.Round(ValorUtils.TruncateDecimal(item.ValorPIS.GetValueOrDefault(), 4), 2)
               }
            };
         else if (item.idCSTPIS > 3 && item.idCSTPIS <= 9)
            det.imposto.PIS = new PIS
            {
               TipoPIS = new PISNT
               {
                  CST = (CSTPIS)item.idCSTPIS.GetValueOrDefault()
               }
            };
         else if(item.idCSTPIS>=49)
         {
            det.imposto.PIS = new PIS
            {
               TipoPIS = new PISOutr
               {
                  CST = (CSTPIS)item.idCSTPIS.GetValueOrDefault(),
                  pPIS = item.ValorAliquotaPIS.GetValueOrDefault(),
                  vBC = item.ValorBaseCalculoPIS.GetValueOrDefault(),
                  vPIS = Math.Round(ValorUtils.TruncateDecimal(item.ValorPIS.GetValueOrDefault(), 4), 2)
               }
            };
         }
         else
         det.imposto.PIS = new PIS
         {
            TipoPIS = new PISNT
            {
               CST = CSTPIS.pis08
            }
         };

         //informar cofins na NFe
         if (item.idCSTCOFINS <= 2)
            det.imposto.COFINS = new COFINS
            {
               TipoCOFINS = new COFINSAliq
               {
                  CST = (CSTCOFINS)item.idCSTCOFINS.GetValueOrDefault(),
                  pCOFINS = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaCOFINS.GetValueOrDefault(), 4), 2),
                  vBC = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoCOFINS.GetValueOrDefault(), 4), 2),
                  vCOFINS = Math.Round(ValorUtils.TruncateDecimal(item.ValorCOFINS.GetValueOrDefault(), 4), 2)
               }
            };
         else if(item.idCSTCOFINS == 3)
            det.imposto.COFINS = new COFINS
            {
               TipoCOFINS = new COFINSQtde
               {
                  CST = (CSTCOFINS)item.idCSTCOFINS.GetValueOrDefault(),
                  qBCProd =item.Quantidade,
                  vAliqProd = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaCOFINS.GetValueOrDefault(), 4), 2),
                  vCOFINS = Math.Round(ValorUtils.TruncateDecimal(item.ValorCOFINS.GetValueOrDefault(), 4), 2)
               }
            };
         else if (item.idCSTCOFINS >=4 && item.idCSTCOFINS <=9)
            det.imposto.COFINS = new COFINS
            {
               TipoCOFINS = new COFINSNT
               {
                  CST = (CSTCOFINS)item.idCSTCOFINS.GetValueOrDefault(),
               }
            };
         else if (item.idCSTCOFINS >= 49)
            det.imposto.COFINS = new COFINS
            {
               TipoCOFINS = new COFINSOutr
               {
                  CST = (CSTCOFINS)item.idCSTCOFINS.GetValueOrDefault(),
                  pCOFINS = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaCOFINS.GetValueOrDefault(), 4), 2),
                  vBC = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoCOFINS.GetValueOrDefault(), 4), 2),
                  vCOFINS = Math.Round(ValorUtils.TruncateDecimal(item.ValorCOFINS.GetValueOrDefault(), 4),2)
               }
            };
         else
            det.imposto.COFINS = new COFINS
            {
               TipoCOFINS = new COFINSNT
               {
                  CST = CSTCOFINS.cofins08,
               }
            };

         //det.imposto.COFINSST = new COFINSST
         //{
         //   vBC = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoCOFINS.GetValueOrDefault(), 4), 2),
         //   pCOFINS = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaCOFINS.GetValueOrDefault(), 4), 2),
         //   vCOFINS = Math.Round(ValorUtils.TruncateDecimal(item.ValorCOFINS.GetValueOrDefault(), 4), 2)
         //};

         //partilha icms
            if (item.idPartilhaICMS.HasValue)
               det.imposto.ICMSUFDest = new ICMSUFDest
               {
                  pFCPUFDest = Math.Round(item.PorcentagemFCP.GetValueOrDefault(), 2),
                  pICMSInter = Math.Round(item.PorcentagemInterestadual.GetValueOrDefault(), 2),
                  pICMSInterPart = Math.Round(item.PorcentagemDestinoPartilha.GetValueOrDefault(), 2),
                  pICMSUFDest = Math.Round(item.PorcentagemIntraestadual.GetValueOrDefault(), 2),
                  vBCUFDest = Math.Round((item.ValorBaseCalculoPartilha.GetValueOrDefault()), 2),
                  vBCFCPUFDest = Math.Round((item.ValorBaseCalculoPartilha.GetValueOrDefault()), 2),
                  vFCPUFDest = Math.Round((item.ValorFCP.GetValueOrDefault()), 2),
                  vICMSUFDest = Math.Round(item.ValorIntraestadual.GetValueOrDefault(), 2),
                  vICMSUFRemet = Math.Round(item.ValorInterestadual.GetValueOrDefault(), 2)
               };

         if (modelo == ModeloDocumento.NFe) //NFCe não aceita grupo "IPI"
         {

            if (item.idCSTIPI.In(50, 99))
            {
               det.imposto.IPI = new IPI()
               {
                  cEnq = 999,
                  TipoIPI = new IPITrib()
                  {
                     CST = (CSTIPI)item.idCSTIPI.Value,
                     vBC = Math.Round(item.ValorBaseCalculoIPI.GetValueOrDefault(), 2),
                     pIPI = Math.Round(item.ValorAliquotaIPI.GetValueOrDefault(), 2),
                     vIPI = Math.Round(item.ValorIPI.GetValueOrDefault(), 2)
                  }
               };
            }
            else if (item.idCSTIPI.In(51, 52, 53, 54, 55))
            {
               int CENQ;
               if (item.idCSTIPI.Value.ToString().PadLeft(2, '0') == "2" || item.idCSTIPI.Value.ToString().PadLeft(2, '0') == "52")
                  CENQ = 301;
               else if (item.idCSTIPI.Value.ToString().PadLeft(2, '0') == "4" || item.idCSTIPI.Value.ToString().PadLeft(2, '0') == "54")
                  CENQ = 001;
               else if (item.idCSTIPI.Value.ToString().PadLeft(2, '0') == "5" || item.idCSTIPI.Value.ToString().PadLeft(2, '0') == "55")
                  CENQ = 101;
               else
                  CENQ = 999;

               det.imposto.IPI = new IPI()
               {
                  cEnq = CENQ,

                  TipoIPI = new IPINT()
                  {
                     CST = (CSTIPI)item.idCSTIPI.Value,
                  }
               };
            }
         }
         if (parametro.Industrial == false && item.tb_nfe.TipoNota == EnumTipoNota.DevolucaoCompra)
         {
            det.impostoDevol = new impostoDevol()
            {
               IPI = new IPIDevolvido() { vIPIDevol = Math.Round(item.ValorIPIDevolv.GetValueOrDefault(), 2) },
               pDevol = Math.Round(item.PercentualIPIDevolv.GetValueOrDefault(), 2)
            };

         }

         var declaracao = item.tb_nfe_declaracao_importacao != null ? item.tb_nfe_declaracao_importacao.FirstOrDefault() : null;
         if (declaracao != null)
         {
            det.imposto.II = new II
            {
               vBC = item.ValorBCII.GetValueOrDefault(),
               vII = item.ValorII.GetValueOrDefault(),
               vDespAdu = item.ValorDespesasAduaneiras.GetValueOrDefault(),
               vIOF = item.ValorIOF.GetValueOrDefault()
            };
            List<PCInfo.Plus.Business.Classes.Informacoes.Detalhe.DeclaracaoImportacao.DI> DeclaracaoI = new List<PCInfo.Plus.Business.Classes.Informacoes.Detalhe.DeclaracaoImportacao.DI>();
            var I = new Classes.Informacoes.Detalhe.DeclaracaoImportacao.DI
            {
               nDI = declaracao.NumeroDI,
               dDI = declaracao.DataRegistroDI,
               xLocDesemb = declaracao.LocalDesembarque,
               UFDesemb = declaracao.UFDesembarque,
               dDesemb = declaracao.DataOcorrenciaDesembarque.ToString().ToDateTime(),
               tpViaTransp = (TipoTransporteInternacional)declaracao.TipoViaTransporte,
               vAFRMM = declaracao.ValorAFRMM.GetValueOrDefault(),
               tpIntermedio = (TipoIntermediacao)declaracao.FormaImportacao,
               CNPJ = declaracao.CNPJAdquirente,
               UFTerceiro = declaracao.UFAdquirente,
               cExportador = declaracao.CodigoExportador,
               adi = GetAdicao(declaracao.tb_nfe_declaracao_importacao_adicao.ToList())
            };
            DeclaracaoI.Add(I);
            det.prod.DI = DeclaracaoI;
         }

         return det;
      }
      protected virtual List<Classes.Informacoes.Detalhe.DeclaracaoImportacao.adi> GetAdicao(List<tb_nfe_declaracao_importacao_adicao> adicao)
      {
         List<Classes.Informacoes.Detalhe.DeclaracaoImportacao.adi> adi = new List<Classes.Informacoes.Detalhe.DeclaracaoImportacao.adi>();
         foreach (var item in adicao)
         {
            var a = new Classes.Informacoes.Detalhe.DeclaracaoImportacao.adi
            {
               nAdicao = item.NumeroAdicao,
               nSeqAdic = item.NumeroSequencialAdicao,
               cFabricante = item.CodigoFabricante,
               vDescDI = item.ValorDescontoItemAdicao > 0 ? item.ValorDescontoItemAdicao : null,
               nDraw = item.NumeroDrawBack

            };
            adi.Add(a);
         }
         return adi;
      }

      protected virtual prod GetProduto(tb_nfe_produto item)
      {
         if (item.tb_produto == null)
            item.tb_produto = new ProdutoBusiness().BuscarPorId(item.idProduto);
         int[] arrayCsosn = new int[] { 201, 202, 203, 500, 900 };
         int[] arrayCst = new int[] { 10, 30, 60, 70, 90 };
         string CESTcadastro = "";
         if (item.idCSOSN != null)
         {
            if (Array.Exists(arrayCsosn, element => element == item.idCSOSN))
               CESTcadastro = item.tb_produto.CEST;
         }
         else if (item.idCSTICMS != null)
         {
            if (Array.Exists(arrayCst, element => element == item.idCSTICMS))
               CESTcadastro = item.tb_produto.CEST;
         }
         item.tb_produto.tb_unidade = new UnidadeBusiness().BuscarPorId(item.tb_produto.idUnidade);
         var codigoANP = item.tb_produto.CodigoANP;
         var complementoDescricao = "";
         if(item.ComplementoDescricao!=null)
         complementoDescricao = item.ComplementoDescricao != string.Empty?" "+ RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(item.ComplementoDescricao.ToUpper())).Trim():string.Empty;
         var p = new prod
         {
            cProd = parametro.tb_nf_configuracao.IdentificacaoProduto == 2 && !string.IsNullOrEmpty(item.tb_produto.CodigoReferencia) ?
            item.tb_produto.CodigoReferencia : item.idProduto.ToString(),
            CEST = CESTcadastro != string.Empty ? CESTcadastro : null,
            cEAN = item.tb_produto.CodigoBarras.ToStringOrEmpty(),           
            xProd = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(item.tb_produto.Descricao.ToUpper())).Trim() + complementoDescricao,
            NCM = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(item.tb_produto.NCM)).Trim(),
            CFOP = item.idCFOP.Value,
            uCom = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(item.tb_produto.tb_unidade.Unidade)).Trim(),
            qCom = Math.Round(item.Quantidade, 4),
            vUnCom = Math.Round(item.ValorUnitario, 4),
            vProd = Math.Round(ValorUtils.TruncateDecimal(item.ValorUnitario * item.Quantidade, 4), 2),
            vDesc = Math.Round(ValorUtils.TruncateDecimal(item.ValorDesconto.GetValueOrDefault(), 4), 2),
            vSeg = Math.Round(ValorUtils.TruncateDecimal(item.ValorSeguro.GetValueOrDefault(), 4), 2),
            vOutro = Math.Round(ValorUtils.TruncateDecimal(item.ValorOutrasDespesas.GetValueOrDefault(), 4), 2),
            vFrete = Math.Round(ValorUtils.TruncateDecimal(item.ValorFrete.GetValueOrDefault(), 4), 2),
            cEANTrib = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(item.tb_produto.CodigoBarras.ToStringOrEmpty())).Trim(),
            uTrib = RemoveCaracteresEspeciaisNFe(StringUtis.SubstituirCarecteresAcentuados(item.tb_produto.tb_unidade.Unidade)).Trim(),
            qTrib = item.Quantidade,
            vUnTrib = Math.Round(item.ValorUnitario, 2),
            indTot = IndicadorTotal.ValorDoItemCompoeTotalNF,
            xPed = item.Pedido != null && item.Pedido != string.Empty ? item.Pedido : "0",
            nItemPed = item.CodPedido != null ? item.CodPedido : 0,
            //ProdutoEspecifico = codigoANP != null ? GetProdutoEspecificoCombustivel(item) : null
            //NVE = {"AA0001", "AB0002", "AC0002"},
            //CEST = ?

            //ProdutoEspecifico = new arma
            //{
            //    tpArma = TipoArma.UsoPermitido,
            //    nSerie = "123456",
            //    nCano = "123456",
            //    descr = "TESTE DE ARMA"
            //}
         };
         return p;
      }

      protected virtual comb GetProdutoEspecificoCombustivel(tb_nfe_produto item)
      {
         var cliente = new ClienteBusiness().BuscarPorId(item.tb_nfe.idCliente);

         var c = new comb
         {
            cProdANP = item.tb_produto.CodigoANP,
            descANP = RemoveCaracteresEspeciaisNFe(item.tb_produto.DescricaoANP.ToString()),
            UFCons = new EstadoBusiness().BuscarPorId(cliente.tb_cidade.tb_estado.id).Sigla
         };
         return c;
      }

      protected virtual ICMSBasico InformarICMS(Csticms CST, tb_nfe_produto item)
      {
         switch (CST)
         {
            case Csticms.Cst00:
               return new ICMS00
               {
                  orig = (OrigemMercadoria)item.idOrigemMercadoria.GetValueOrDefault(),
                  CST = (Csticms)item.idCSTICMS.GetValueOrDefault(),
                  modBC = (DeterminacaoBaseIcms)3,
                  vBC = item.ValorBaseCalculoICMS.GetValueOrDefault(),
                  pICMS = item.ValorAliquotaICMS.GetValueOrDefault(),
                  vICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMS.GetValueOrDefault(), 4), 2),
                  pFCP = /*item.PorcentagemFCP != 0 ? item.PorcentagemFCP :*/ null,
                  vFCP = /*item.ValorFCP != 0 ? item.ValorFCP :*/ null,
               };
            case Csticms.Cst10:
               return new ICMS10
               {
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
                  CST = (Csticms)item.idCSTICMS.Value,
                  modBC = (DeterminacaoBaseIcms)0,
                  vBC = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMS.GetValueOrDefault(), 4), 2),
                  pICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaICMS.GetValueOrDefault(), 4), 2),
                  vICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMS.GetValueOrDefault(), 4), 2),
                  modBCST = (DeterminacaoBaseIcmsSt)4,
                  pMVAST = item.MVA.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.MVA.GetValueOrDefault(), 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  pRedBCST = item.PorcentagemReducaoST.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducaoST.Value, 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  vBCST = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMSST.GetValueOrDefault(), 4), 2),
                  pICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaST.GetValueOrDefault(), 4), 2),
                  vICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMSST.GetValueOrDefault(), 4), 2),
               };
            case Csticms.Cst20:
               return new ICMS20
               {
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
                  CST = (Csticms)item.idCSTICMS.Value,
                  modBC = (DeterminacaoBaseIcms)3,
                  pRedBC = item.PorcentagemReducao.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducao.Value, 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  vBC = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMS.GetValueOrDefault(), 4), 2),
                  pICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaICMS.GetValueOrDefault(), 4), 2),
                  vICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMS.GetValueOrDefault(), 4), 2),
               };
            case Csticms.Cst30:
               return new ICMS30
               {
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
                  CST = (Csticms)item.idCSTICMS.Value,
                  modBCST = (DeterminacaoBaseIcmsSt)4,
                  pMVAST = Math.Round(ValorUtils.TruncateDecimal(item.MVA.GetValueOrDefault(), 4), 2),
                  pRedBCST = item.PorcentagemReducaoST.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducaoST.Value, 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  vBCST = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMSST.GetValueOrDefault(), 4), 2),
                  pICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaST.GetValueOrDefault(), 4), 2),
                  vICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMSST.GetValueOrDefault(), 4), 2),
               };
            case Csticms.Cst40:
            case Csticms.Cst41:
            case Csticms.Cst50:
               return new ICMS40
               {
                  CST = (Csticms)item.idCSTICMS.Value,
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
               };
            case Csticms.Cst51:
               return new ICMS51
               {
                  CST = (Csticms)item.idCSTICMS.Value,
                  orig = (OrigemMercadoria)item.idOrigemMercadoria.Value,
                  modBC = (DeterminacaoBaseIcms)3,
                  pRedBC = item.PorcentagemReducao.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducao.Value, 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  vBC = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMS.GetValueOrDefault(), 4), 2),
                  pICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaICMS.GetValueOrDefault(), 4), 2),
                  vICMSOp = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMS.GetValueOrDefault() * (item.ValorAliquotaICMS.GetValueOrDefault() / 100), 4), 2),
                  pDif = Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemDiferimento.GetValueOrDefault(), 4), 2),
                  vICMSDif = Math.Round(ValorUtils.TruncateDecimal(item.ValorIcmsDiferido.GetValueOrDefault(), 4), 2),
                  vICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMS.GetValueOrDefault(), 4), 2),
               };
            case Csticms.Cst60:
               return new ICMS60
               {
                  orig = (OrigemMercadoria)item.idOrigemMercadoria.Value,
                  CST = (Csticms)item.idCSTICMS.Value,
                  vBCSTRet = item.ValorBaseCalculoICMSSTRet.GetValueOrDefault(),
                  pST = item.ValorAliquotaSTRet.GetValueOrDefault(),
                  vICMSSTRet = item.ValorICMSSTRetido.GetValueOrDefault(),
               };
            case Csticms.Cst70:
               return new ICMS70
               {
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
                  CST = (Csticms)item.idCSTICMS.Value,
                  modBC = (DeterminacaoBaseIcms)0,
                  pRedBC = item.PorcentagemReducao.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducao.Value, 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  vBC = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMS.GetValueOrDefault(), 4), 2),
                  pICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaICMS.GetValueOrDefault(), 4), 2),
                  vICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMS.GetValueOrDefault(), 4), 2),
                  modBCST = (DeterminacaoBaseIcmsSt)4,
                  pMVAST = item.MVA.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.MVA.GetValueOrDefault(), 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  pRedBCST = item.PorcentagemReducaoST.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducaoST.Value, 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  vBCST = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMSST.GetValueOrDefault(), 4), 2),
                  pICMSST = item.ValorAliquotaST.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaST.Value, 4), 2) : Math.Round("0,0000".ToDecimal(), 4),
                  vICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMSST.GetValueOrDefault(), 4), 2),
               };
            case Csticms.Cst90:
               return new ICMS90
               {
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
                  CST = (Csticms)item.idCSTICMS.Value,
                  modBC = (DeterminacaoBaseIcms)3,
                  vBC = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMS.Value, 4), 2),
                  pRedBC = Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducao.GetValueOrDefault(), 4), 2),
                  pICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaICMS.GetValueOrDefault(), 4), 2),
                  vICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMS.GetValueOrDefault(), 4), 2),
                  vBCFCP = null,
                  pMVAST = Math.Round(ValorUtils.TruncateDecimal(item.MVA.GetValueOrDefault(), 4), 2),
                  pFCP = null,
                  vFCP = null,
                  modBCST = (DeterminacaoBaseIcmsSt)4,
                  vICMSDeson = null,
                  motDesICMS = null,//(MotivoDesoneracaoIcms)9,
                  pRedBCST = Math.Round(item.PorcentagemReducaoST.GetValueOrDefault(), 4),
                  vBCST = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMSST.GetValueOrDefault(), 4), 2),
                  pICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaST.GetValueOrDefault(), 4), 2),
                  vICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMSST.GetValueOrDefault(), 4), 2),
               };
            default:
               return new ICMS00();
         }

      }

      protected virtual ICMSBasico ObterIcmsBasico(CRT crt)
      {
         //Leia os dados de seu banco de dados e em seguida alimente o objeto ICMSGeral, como no exemplo abaixo.
         var icmsGeral = new ICMSGeral
         {
            orig = OrigemMercadoria.OmNacional,
            CST = Csticms.Cst00,
            modBC = DeterminacaoBaseIcms.DbiValorOperacao,
            vBC = 1.1m,
            pICMS = 18,
            vICMS = 0.20m,
            motDesICMS = MotivoDesoneracaoIcms.MdiTaxi
         };
         return icmsGeral.ObterICMSBasico(crt);
      }

      private PISBasico ObterPisBasico()
      {
         //Leia os dados de seu banco de dados e em seguida alimente o objeto PISGeral, como no exemplo abaixo.
         var pisGeral = new PISGeral()
         {
            CST = CSTPIS.pis01,
            vBC = 1.1m,
            pPIS = 1.65m,
            vPIS = 0.01m,
            vAliqProd = 0
         };

         return pisGeral.ObterPISBasico();
      }
      private COFINSBasico ObterCofinsBasico()
      {
         //Leia os dados de seu banco de dados e em seguida alimente o objeto COFINSGeral, como no exemplo abaixo.
         var cofinsGeral = new COFINSGeral()
         {
            CST = CSTCOFINS.cofins01,
            vBC = 1.1m,
            pCOFINS = 1.65m,
            vCOFINS = 0.01m,
            vAliqProd = 0
         };

         return cofinsGeral.ObterCOFINSBasico();
      }
      private IPIBasico ObterIPIBasico()
      {
         //Leia os dados de seu banco de dados e em seguida alimente o objeto IPIGeral, como no exemplo abaixo.
         var ipiGeral = new IPIGeral()
         {
            CST = CSTIPI.ipi01,
            vBC = 1.1m,
            pIPI = 5m,
            vIPI = 0.05m
         };

         return ipiGeral.ObterIPIBasico();
      }
      protected virtual ICMSBasico InformarCSOSN(Csosnicms CST, tb_nfe_produto item)
      {
         switch (CST)
         {
            case Csosnicms.Csosn101:
               return new ICMSSN101
               {
                  CSOSN = Csosnicms.Csosn101,
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
                  pCredSN = Math.Round(ValorUtils.TruncateDecimal(item.AliquotaCreditoICMS.GetValueOrDefault(), 4), 2),
                  vCredICMSSN = Math.Round(ValorUtils.TruncateDecimal(item.ValorCreditoICMS.GetValueOrDefault(), 4), 2)
               };

            case Csosnicms.Csosn102:
            case Csosnicms.Csosn103:
            case Csosnicms.Csosn300:
            case Csosnicms.Csosn400:
               return new ICMSSN102
               {
                  CSOSN = (Csosnicms)item.idCSOSN,
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
               };

            case Csosnicms.Csosn201:
               return new ICMSSN201
               {

                  CSOSN = (Csosnicms)item.idCSOSN.Value,
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
                  modBCST = (DeterminacaoBaseIcmsSt)4,
                  pMVAST = Math.Round(ValorUtils.TruncateDecimal(item.MVA.GetValueOrDefault(), 4), 2),
                  pRedBCST = item.PorcentagemReducaoST.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducaoST.Value, 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  vBCST = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMSST.GetValueOrDefault(), 4), 2),
                  pICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaST.GetValueOrDefault(), 4), 2),
                  vICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMSST.GetValueOrDefault(), 4), 2),
                  pCredSN = item.AliquotaCreditoICMS.GetValueOrDefault(),
                  vCredICMSSN = item.ValorCreditoICMS.GetValueOrDefault(),
               };

            case Csosnicms.Csosn202:
            case Csosnicms.Csosn203:
               return new ICMSSN202
               {
                  CSOSN = (Csosnicms)item.idCSOSN.GetValueOrDefault(),
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
                  modBCST = (DeterminacaoBaseIcmsSt)4,
                  pRedBCST = Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducaoST.GetValueOrDefault(), 4), 2),
                  pMVAST = item.MVA.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.MVA.Value, 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  pICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaST.GetValueOrDefault(), 4), 2),
                  vBCST = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMSST.GetValueOrDefault(), 4), 2),
                  vICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMSST.GetValueOrDefault(), 4), 2)
               };
            case Csosnicms.Csosn500:
               return new ICMSSN500
               {
                  CSOSN = (Csosnicms)item.idCSOSN.Value,
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
                  vBCSTRet = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMSST.GetValueOrDefault(), 2), 2),
                  vICMSSTRet = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMSST.GetValueOrDefault(), 2), 2),
                  pST = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaST.GetValueOrDefault(), 4), 2)
               };
            case Csosnicms.Csosn900:
               return new ICMSSN900
               {
                  CSOSN = (Csosnicms)item.idCSOSN.GetValueOrDefault(),
                  orig = (OrigemMercadoria)item.idOrigemMercadoria,
                  modBC = (DeterminacaoBaseIcms)3,
                  pRedBC = Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducao.GetValueOrDefault(), 4), 2),
                  vBC = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMS.GetValueOrDefault(), 4), 2),
                  pICMS = Math.Round(ValorUtils.TruncateDecimal(item.AliquotaCreditoICMS.GetValueOrDefault(), 4), 2),
                  vICMS = Math.Round(ValorUtils.TruncateDecimal(item.ValorCreditoICMS.GetValueOrDefault(), 4), 2),
                  modBCST = (DeterminacaoBaseIcmsSt)4,
                  pMVAST = item.MVA.HasValue ? Math.Round(ValorUtils.TruncateDecimal(item.MVA.Value, 4), 2) : Math.Round("0,00".ToDecimal(), 2),
                  pRedBCST = Math.Round(ValorUtils.TruncateDecimal(item.PorcentagemReducaoST.GetValueOrDefault(), 4), 2),
                  vBCST = Math.Round(ValorUtils.TruncateDecimal(item.ValorBaseCalculoICMSST.GetValueOrDefault(), 4), 2),
                  pICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorAliquotaST.GetValueOrDefault(), 4), 2),
                  vICMSST = Math.Round(ValorUtils.TruncateDecimal(item.ValorICMSST.GetValueOrDefault(), 4), 2),
                  pCredSN = item.AliquotaCreditoICMS.GetValueOrDefault(),
                  vCredICMSSN = item.ValorCreditoICMS.GetValueOrDefault()
               };
            default:
               return new ICMSSN201();
         }
      }

      protected virtual totalizador GetTotal(VersaoServico versao, List<det> produtos, ICollection<tb_nfe_produto> item)
      {
         var icmsTot = new ICMSTot();

         if (item.Where(x => x.idCSOSN.HasValue).ToList().Count() > 0)
         {
            icmsTot.vBC = Math.Round(item.Where(x => x.idCSOSN.GetValueOrDefault() != 201).Sum(x => Math.Round(x.ValorBaseCalculoICMS.GetValueOrDefault(), 2)), 2);

            if (icmsTot.vBC == 0)
               icmsTot.vBC = Math.Round(item.Where(x => x.idCSOSN.GetValueOrDefault() == 900).Sum(x => Math.Round(valorBaseCreditoICMS, 2)), 2);

            if (icmsTot.vICMS == 0)
            {
               icmsTot.vICMS = Math.Round(item.Where(x => x.idCSOSN.GetValueOrDefault() != 201 && x.idCSOSN.GetValueOrDefault() == 900).Sum(x => Math.Round(x.ValorCreditoICMS.GetValueOrDefault(), 2)), 2);
               icmsTot.vBC = Math.Round(item.Where(x => x.idCSOSN.GetValueOrDefault() != 201 && x.idCSOSN.GetValueOrDefault() == 900).Sum(x => Math.Round(x.ValorBaseCalculoICMS.GetValueOrDefault(), 2)), 2);
            }
         }
         else
         {
            icmsTot.vBC = Math.Round(item.Where(x =>
            x.idCSTICMS.GetValueOrDefault() != 30 && x.idCSTICMS.GetValueOrDefault() != 40 && x.idCSTICMS.GetValueOrDefault() != 50
            && x.idCSTICMS.GetValueOrDefault() != 60
            ).Sum(x => Math.Round(x.ValorBaseCalculoICMS.GetValueOrDefault(), 2)), 2);

            icmsTot.vICMS = Math.Round(item.Where(x => x.idCSOSN.GetValueOrDefault() != 201).Sum(x => Math.Round(x.ValorICMS.GetValueOrDefault(), 2)), 2);
         }
         var valorTotalICMSIntraestadual = item.Sum(x => Math.Round(x.ValorIntraestadual.GetValueOrDefault(), 2));
         var valorTotalICMSInterestadual = item.Sum(x => x.ValorInterestadual.GetValueOrDefault());
         var valorIcmsSt = item.Sum(x => x.ValorICMSST.GetValueOrDefault());
         if (valorIcmsSt > 0)
         {
            icmsTot.vBCST = Math.Round(item.Sum(x => Math.Round(x.ValorBaseCalculoICMSST.GetValueOrDefault(), 2)), 2);
            icmsTot.vST = Math.Round(item.Sum(x => Math.Round(x.ValorICMSST.GetValueOrDefault(), 2)), 2);
         }

         ////Adicionar para o cst 40 - de minas gerais para são paulo
         icmsTot.vICMSDeson = 0;
         icmsTot.vFCPUFDest = Math.Round(item.Sum(x => Math.Round(x.ValorFCP.GetValueOrDefault(), 2)), 2);

         icmsTot.vProd = Math.Round(item.Sum(x => Math.Round(x.ValorUnitario * x.Quantidade, 4)), 2);
         icmsTot.vFrete = Math.Round(item.Sum(x => Math.Round(x.ValorFrete.GetValueOrDefault(), 2)), 2);
         icmsTot.vSeg = Math.Round(item.Sum(x => Math.Round(x.ValorSeguro.GetValueOrDefault(), 2)), 2);
         if (item.Sum(x => x.ValorII.GetValueOrDefault()) > 0)
            icmsTot.vII = Math.Round(item.Sum(x => Math.Round(x.ValorII.GetValueOrDefault(), 2)), 2);
         icmsTot.vDesc = Math.Round(item.Sum(x => Math.Round(x.ValorDesconto.GetValueOrDefault(), 2)), 4);

         icmsTot.vOutro = Math.Round(item.Sum(x => Math.Round(x.ValorOutrasDespesas.GetValueOrDefault(), 2)), 2);
         icmsTot.vIPI = Math.Round(item.Sum(x => Math.Round(x.ValorIPI.GetValueOrDefault(), 2)), 2);
         icmsTot.vIPIDevol = Math.Round(item.Sum(x => Math.Round(x.ValorIPIDevolv.GetValueOrDefault(), 2)), 2);
         icmsTot.vPIS = Math.Round(item.Sum(x => Math.Round(x.ValorPIS.GetValueOrDefault(), 2)), 2);
         icmsTot.vCOFINS = Math.Round(item.Sum(x => Math.Round(x.ValorCOFINS.GetValueOrDefault(), 2)), 2);
         decimal total = (item.Sum(x => x.ValorTotalProduto.GetValueOrDefault() + x.ValorIPIDevolv.GetValueOrDefault()));
         icmsTot.vNF = total.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO).ToDecimal();
         icmsTot.vTotTrib = 0;

         bool CST = item.FirstOrDefault().idCSTICMS.In(00, 20, 40, 41);
         if (valorTotalICMSIntraestadual > 0)
            icmsTot.vICMSUFDest = CST == true ? Math.Round(Math.Round(valorTotalICMSIntraestadual, 2), 2) : 0;

         if (valorTotalICMSInterestadual >= 0 && CST)
            icmsTot.vICMSUFRemet = CST == true ? Math.Round(Math.Round(valorTotalICMSInterestadual, 2), 2) : 0;
         icmsTot.vFCP = 0;
         icmsTot.vFCPST = 0;
         icmsTot.vFCPSTRet = 0;
         //** Regra de validação W16-10 que rege sobre o Total da NF **//
         icmsTot.vNF =
             icmsTot.vProd
             - icmsTot.vDesc
             - icmsTot.vICMSDeson.GetValueOrDefault()
             + icmsTot.vST
             + icmsTot.vFCPST.GetValueOrDefault()
             + icmsTot.vFrete
             + icmsTot.vSeg
             + icmsTot.vOutro
             + icmsTot.vII
             + icmsTot.vIPI
             + icmsTot.vIPIDevol.GetValueOrDefault();

         var t = new totalizador { ICMSTot = icmsTot };
         return t;
      }

      private Classes.NFe Assina(Classes.NFe nfeClasse, ConfiguracaoServico cfgServico = null, X509Certificate2 _certificado = null)
      {
         var nfeLocal = nfeClasse;
         if (nfeLocal == null) throw new ArgumentNullException("nfe");

         #region Define cNF

         var tamanhocNf = 9;
         var versao = (decimal.Parse(nfeLocal.infNFe.versao, CultureInfo.InvariantCulture));
         if (versao >= 2) tamanhocNf = 8;
         nfeLocal.infNFe.ide.cNF = Convert.ToInt32(nfeLocal.infNFe.ide.cNF).ToString().PadLeft(tamanhocNf, '0');

         #endregion

         var modeloDocumentoFiscal = nfeLocal.infNFe.ide.mod;
         var tipoEmissao = (int)nfeLocal.infNFe.ide.tpEmis;
         var codigoNumerico = int.Parse(nfeLocal.infNFe.ide.cNF);
         var estado = nfeLocal.infNFe.ide.cUF;
         var dataEHoraEmissao = nfeLocal.infNFe.ide.dhEmi;
         var cnpj = nfeLocal.infNFe.emit.CNPJ;
         var numeroDocumento = nfeLocal.infNFe.ide.nNF;
         var serie = nfeLocal.infNFe.ide.serie;

         var dadosChave = ChaveFiscal.ObterChave(estado, dataEHoraEmissao.LocalDateTime, cnpj, modeloDocumentoFiscal, serie, numeroDocumento, tipoEmissao, codigoNumerico);

         nfeLocal.infNFe.Id = "NFe" + dadosChave.Chave;
         nfeLocal.infNFe.ide.cDV = Convert.ToInt16(dadosChave.DigitoVerificador);
         nfe.Chave = dadosChave.Chave;
         Signature assinatura = null;
         if (_certificado == null)
            assinatura = Assinador.ObterAssinatura(nfeLocal, nfeLocal.infNFe.Id, cfgServico);
         else
            assinatura = Assinador.ObterAssinatura(nfeLocal, nfeLocal.infNFe.Id, _certificado, cfgServico.Certificado.ManterDadosEmCache, cfgServico.Certificado.SignatureMethodSignedXml, cfgServico.Certificado.DigestMethodReference);
         nfeLocal.Signature = assinatura;
         return nfeLocal;
      }
      private Classes.NFe Valida(Classes.NFe nfe, ConfiguracaoServico cfgServico = null)
      {
         if (nfe == null) throw new ArgumentNullException("nfe");

         var versao = (Decimal.Parse(nfe.infNFe.versao, CultureInfo.InvariantCulture));

         var xmlNfe = ObterXmlString(nfe);
         var config = cfgServico ?? ConfiguracaoServico.Instancia;
         if (versao < 3)
            Validador.Valida(ServicoNFe.NfeRecepcao, config.VersaoNfeRecepcao, xmlNfe, false, config);
         if (versao >= 3)
            Validador.Valida(ServicoNFe.NFeAutorizacao, config.VersaoNFeAutorizacao, xmlNfe, false, config);

         return nfe; //Para uso no formato fluent
      }
      public static string ObterXmlString(PCInfo.Plus.Business.Classes.NFe nfe)
      {
         return FuncoesXml.ClasseParaXmlString(nfe);
      }
      #endregion
      private bool NotaImportacao(tb_nfe nfe)
      {
         if (nfe.TipoNota == (int)EnumTipoNota.Entrada)
         {
            var cliente = new ClienteBusiness().BuscarPorId(nfe.idCliente);
            if (cliente != null && cliente.tb_cidade.tb_estado.idPais != Constants.ID_PAIS_BRASIL)
               return true;
            else
               return false;
         }
         return false;
      }

      public string PreencherImpostosAproximados(List<tb_nfe_produto> listaProduto)
      {
         var impostoAproximadoBusiness = new ImpostoAproximadoBusiness();
         var produtoBusiness = new ProdutoBusiness();
         string impostosAproximados = "Impostos aproximados: R$ {0} ({1}) Nacional, R$ {2} ({3}) Federal, R$ {4} ({5}) Estadual, R$ {6} ({7}) Municipal - Fonte IBPT Conforme Lei: 12.741/2012";
         decimal valorNacionalFederal = 0;
         decimal valorImportadoFederal = 0;
         decimal valorEstadual = 0;
         decimal valorMunicipal = 0;
         decimal porcentagemNacional = 0;
         decimal porcentagemFederal = 0;
         decimal porcentagemEstadual = 0;
         decimal porcentagemMunicipal = 0;

         foreach (var produto in listaProduto)
         {
            var prod = produtoBusiness.BuscarPorId(produto.idProduto);
            if (prod != null && !string.IsNullOrEmpty(prod.NCM))
               if (prod.NCM.StartsWith("0")) prod.NCM = prod.NCM.Substring(1);
            var impostoAproximado = impostoAproximadoBusiness.BuscarPorNCM(prod.NCM);
            if (impostoAproximado != null)
            {
               if (DateTime.Now > impostoAproximado.DataVigenciaFinal)
               {
                  MessageBoxUtils.ExibeMensagemAdvertencia("Tabela IBPT desatualizada!\nFavor entrar em contato com o suporte.");
                  return string.Empty;
               }
               //Cálculo federal somente para produtos importados
               if (produto.idOrigemMercadoria == 0 || produto.idOrigemMercadoria == 3 || produto.idOrigemMercadoria == 4 || produto.idOrigemMercadoria == 5)
               {
                  valorNacionalFederal += produto.ValorTotalProduto.GetValueOrDefault() * (impostoAproximado.AliquotaNacional / 100);
                  porcentagemNacional += impostoAproximado.AliquotaNacional;
                  porcentagemFederal += impostoAproximado.AliquotaFederal;
                  valorImportadoFederal += produto.ValorTotalProduto.GetValueOrDefault() * (impostoAproximado.AliquotaFederal / 100);
               }
               else
               {
                  valorImportadoFederal += produto.ValorTotalProduto.GetValueOrDefault() * (impostoAproximado.AliquotaFederal / 100);
                  porcentagemFederal += impostoAproximado.AliquotaFederal;
               }
               valorEstadual += produto.ValorTotalProduto.GetValueOrDefault() * (impostoAproximado.AliquotaEstadual / 100);
               porcentagemEstadual += impostoAproximado.AliquotaEstadual;
               valorMunicipal += produto.ValorTotalProduto.GetValueOrDefault() * (impostoAproximado.AliquotaMunicipal / 100);
               porcentagemMunicipal += impostoAproximado.AliquotaMunicipal;
            }
            else
               continue;
         }

         //Calcular a Média da Porcentagem dos Impostos
         if (porcentagemNacional > 0) porcentagemNacional = Math.Round((porcentagemNacional / listaProduto.Count), 2);
         if (porcentagemFederal > 0) porcentagemFederal = Math.Round((porcentagemFederal / listaProduto.Count), 2);
         if (porcentagemEstadual > 0) porcentagemEstadual = Math.Round((porcentagemEstadual / listaProduto.Count), 2);
         if (porcentagemMunicipal > 0) porcentagemMunicipal = Math.Round((porcentagemMunicipal / listaProduto.Count), 2);

         return StringUtis.SubstituirCarecteresAcentuados(RemoveCaracteresEspeciaisNFe(string.Format(impostosAproximados,
         valorNacionalFederal.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO),
         porcentagemNacional.ToString("F2") + "%",
         valorImportadoFederal.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO),
         porcentagemFederal.ToString("F2") + "%",
         valorEstadual.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO),
         porcentagemEstadual.ToString("F2") + "%",
         valorMunicipal.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO),
         porcentagemMunicipal.ToString("F2") + "%"))).Trim();
      }

      #region EnviarEpec
      public void EnviarEpec(ServicosNFe servicosNfe, int lote, int sequenciaEvento, Classes.NFe nfe, string veraplic, tb_nfe nf)
      {
         var retornoEpec = servicosNfe.RecepcaoEventoEpec(lote.ToInt32(), 1, nfe, veraplic);
         var motivo = string.Empty;
         var codigo = 0;
         if (retornoEpec.RetornoCompletoStr.Contains("Rejeicao") || retornoEpec.Retorno.retEvento.FirstOrDefault().infEvento.nProt == null)
         {
            nf.idNFStatus = (int)EnumStatusNFe.ErroEmissaoNFe;
            nf.XMLRetorno = retornoEpec.RetornoCompletoStr;
            nf.XML = retornoEpec.EnvioStr;
            Salvar(nf);
            motivo = retornoEpec.Retorno.retEvento.FirstOrDefault().infEvento.xMotivo;
            codigo = retornoEpec.Retorno.retEvento.FirstOrDefault().infEvento.cStat;
            new NFErrosBusiness().GravarErro(codigo, motivo, nf);
            throw new NFeException("Falha no envio da NFe. Consulte mais detalhes na tela de Erros da NFe.");
         }
         else if (retornoEpec.Retorno.retEvento.FirstOrDefault().infEvento.nProt != null)
         {
            nf.idNFStatus = (int)EnumStatusNFe.EpecAguardandoEnvio;
            //nf.DigestValue = retornoEpec.Retorno.protNFe.FirstOrDefault().infProt.digVal;
            nf.idLote = lote;
            nf.NumeroProtocolo = retornoEpec.Retorno.retEvento.FirstOrDefault().infEvento.nProt;
            nf.XMLRetorno = retornoEpec.RetornoCompletoStr;
            nf.XML = retornoEpec.EnvioStr;
            nf.idNFStatus = (int)EnumStatusNFe.EpecAguardandoEnvio;
         }

         using (TransactionScope transacao = new TransactionScope())
         {
            Salvar(nf);
            var existeVenda = new VendaNFeBusiness().BuscarPorIdNFe(nf.id);
            if (existeVenda != null)
               new VendaBusiness().AtualizarStatusVenda(existeVenda.idVenda, EnumStatusVenda.Concluida);

            var existeCompra = new CompraNFeBusiness().BuscarPorIdNFe(nf.id);
            if (existeCompra != null)
            {
               new CompraBusiness().AtualizarStatusCompra(existeCompra.idCompra, EnumStatusVenda.Concluida);
               new CompraNFBusiness().AtualizarDadosNFe(existeCompra.idCompra, nf);
            }

            var existeDevolucaoCompra = new DevolucaoCompraNFeBusiness().BuscarPorIdNFe(nf.id);
            if (existeDevolucaoCompra != null)
            {
               var devolucaoCompraBusiness = new DevolucaoCompraBusiness();
               devolucaoCompraBusiness.AtualizarStatusDevolucaoCompra(existeDevolucaoCompra.idDevolucaoCompra, EnumStatusDevolucao.Concluida);
               var devolucaoCompra = devolucaoCompraBusiness.BuscarPorId(existeDevolucaoCompra.idDevolucaoCompra);
               if (devolucaoCompra != null)
                  devolucaoCompraBusiness.CriarMovimentacaoFinanceiroEstoque(devolucaoCompra);
            }

            var existeDevolucaoVenda = new DevolucaoVendaNFeBusiness().BuscarPorIdNFe(nf.id);
            if (existeDevolucaoVenda != null)
            {
               var devolucaoVendaBusiness = new DevolucaoVendaBusiness();
               devolucaoVendaBusiness.AtualizarStatusDevolucaoVenda(existeDevolucaoVenda.idDevolucaoVenda, EnumStatusDevolucao.Concluida);
               var devolucaoVenda = devolucaoVendaBusiness.BuscarPorId(existeDevolucaoVenda.idDevolucaoVenda);
               if (devolucaoVenda != null)
                  devolucaoVendaBusiness.CriarMovimentacaoFinanceiroEstoque(devolucaoVenda);
            }
            transacao.Complete();
         }
      }
      #endregion

      #region EnviarNFe 4.0
      public void EnviarNFe4_0(ServicosNFe servicosNfe, List<NFe> nfe, string lote, tb_nfe nf, X509Certificate2 certificado)
      {
         IndicadorSincronizacao indicadorSincronizacao = empresa.UF.In("BA") ? IndicadorSincronizacao.Assincrono : IndicadorSincronizacao.Sincrono;

         var retornoEnvio = servicosNfe.NFeAutorizacao(Convert.ToInt32(lote), indicadorSincronizacao, nfe, false);

         var motivo = string.Empty;
         var codigo = 0;

         if (empresa.UF.In("BA", "SP"))
         {
            WaitWindow.Begin("Aguarde...");

            System.Threading.Thread.Sleep(10000);
            WaitWindow.End();
            var retornoRecibo = servicosNfe.NFeRetAutorizacao(retornoEnvio.Retorno.infRec.nRec.ToString());

            if (retornoRecibo.RetornoCompletoStr.Contains("Rejeicao") || retornoRecibo.Retorno.protNFe.FirstOrDefault().infProt.nProt == null)
            {
               nf.idNFStatus = (int)EnumStatusNFe.ErroEmissaoNFe;
               nf.XML = retornoEnvio.EnvioStr;
               Salvar(nf);
               motivo = retornoRecibo.Retorno.protNFe.FirstOrDefault().infProt.xMotivo;
               codigo = retornoRecibo.Retorno.protNFe.FirstOrDefault().infProt.cStat;
               new NFErrosBusiness().GravarErro(codigo, motivo, nf);
               throw new NFeException("Falha no envio da NFe. Consulte mais detalhes na tela de Erros da NFe.");

            }
            else if (retornoRecibo.Retorno.protNFe.FirstOrDefault().infProt.nProt != null && retornoRecibo.Retorno.protNFe.FirstOrDefault().infProt.digVal != null)
            {
               nf.idNFStatus = (int)EnumStatusNFe.EmitidaComSucesso;
               nf.DigestValue = retornoRecibo.Retorno.protNFe.FirstOrDefault().infProt.digVal;
               nf.NumeroProtocolo = retornoRecibo.Retorno.protNFe.FirstOrDefault().infProt.nProt;
               nf.idLote = lote.ToIntOrNull();
               nf.XML = retornoEnvio.EnvioStr;
               //incluir o nfeproc no xml. Isso unifica e torna possivel a importação do xml em outros sistemas
               var retornoConsulta = servicosNfe.NfeConsultaProtocolo(retornoRecibo.Retorno.protNFe.FirstOrDefault().infProt.chNFe);
               nf.XMLRetorno = retornoConsulta.RetornoCompletoStr;

               var nfeproc = new nfeProc
               {
                  NFe = nfe.FirstOrDefault(),
                  protNFe = retornoConsulta.Retorno.protNFe,
                  versao = retornoConsulta.Retorno.versao
               };
               if (nfeproc.protNFe != null)
               {
                  var novoArquivo = _configuracoes.CfgServico.DiretorioSalvarXml + @"\" + nfeproc.protNFe.infProt.chNFe +
                                    "-procNfe.xml";
                  FuncoesXml.ClasseParaArquivoXml(nfeproc, novoArquivo);

               }
            }
         }
         else
         {
            if (retornoEnvio.Retorno.protNFe == null)
            {
               nf.idNFStatus = (int)EnumStatusNFe.ErroEmissaoNFe;
               nf.XML = retornoEnvio.EnvioStr;
               Salvar(nf);
               motivo = retornoEnvio.Retorno.xMotivo;
               codigo = retornoEnvio.Retorno.cStat;
               new NFErrosBusiness().GravarErro(codigo, motivo, nf);
               throw new NFeException("Falha no envio da NFe. Consulte mais detalhes na tela de Erros da NFe.");
            }
            else if (retornoEnvio.Retorno.protNFe.infProt.xMotivo != null && retornoEnvio.Retorno.protNFe.infProt.nProt == null)
            {
               nf.idNFStatus = (int)EnumStatusNFe.ErroEmissaoNFe;
               nf.XML = retornoEnvio.EnvioStr;
               Salvar(nf);
               motivo = retornoEnvio.Retorno.protNFe.infProt.xMotivo;
               codigo = retornoEnvio.Retorno.protNFe.infProt.cStat;
               new NFErrosBusiness().GravarErro(codigo, motivo, nf);
               throw new NFeException("Falha no envio da NFe. Consulte mais detalhes na tela de Erros da NFe.");
            }
            else
            {
               nf.idNFStatus = (int)EnumStatusNFe.EmitidaComSucesso;
               nf.DigestValue = retornoEnvio.Retorno.protNFe.infProt.digVal;
               nf.NumeroProtocolo = retornoEnvio.Retorno.protNFe.infProt.nProt;
               nf.idLote = lote.ToIntOrNull();
               nf.XMLRetorno = retornoEnvio.RetornoCompletoStr;
               nf.XML = retornoEnvio.EnvioStr;

               //incluir o nfeproc no xml. Isso unifica e torna possivel a importação do xml em outros sistemas
               var retornoConsulta = servicosNfe.NfeConsultaProtocolo(retornoEnvio.Retorno.protNFe.infProt.chNFe);

               var nfeproc = new nfeProc
               {
                  NFe = nfe.FirstOrDefault(),
                  protNFe = retornoConsulta.Retorno.protNFe,
                  versao = retornoConsulta.Retorno.versao
               };
               if (nfeproc.protNFe != null)
               {
                  var novoArquivo = _configuracoes.CfgServico.DiretorioSalvarXml + @"\" + nfeproc.protNFe.infProt.chNFe +
                                    "-procNfe.xml";
                  FuncoesXml.ClasseParaArquivoXml(nfeproc, novoArquivo);

               }
            }
         }
         using (TransactionScope transacao = new TransactionScope())

         {
            Salvar(nf);
            var existeVenda = new VendaNFeBusiness().BuscarPorIdNFe(nf.id);
            if (existeVenda != null)
               new VendaBusiness().AtualizarStatusVenda(existeVenda.idVenda, EnumStatusVenda.Concluida);

            var existeCompra = new CompraNFeBusiness().BuscarPorIdNFe(nf.id);
            if (existeCompra != null)
            {
               new CompraBusiness().AtualizarStatusCompra(existeCompra.idCompra, EnumStatusVenda.Concluida);
               new CompraNFBusiness().AtualizarDadosNFe(existeCompra.idCompra, nf);
            }

            var existeDevolucaoCompra = new DevolucaoCompraNFeBusiness().BuscarPorIdNFe(nf.id);
            if (existeDevolucaoCompra != null)
            {
               var devolucaoCompraBusiness = new DevolucaoCompraBusiness();
               devolucaoCompraBusiness.AtualizarStatusDevolucaoCompra(existeDevolucaoCompra.idDevolucaoCompra, EnumStatusDevolucao.Concluida);
               var devolucaoCompra = devolucaoCompraBusiness.BuscarPorId(existeDevolucaoCompra.idDevolucaoCompra);
               if (devolucaoCompra != null)
                  devolucaoCompraBusiness.CriarMovimentacaoFinanceiroEstoque(devolucaoCompra);
            }

            var existeDevolucaoVenda = new DevolucaoVendaNFeBusiness().BuscarPorIdNFe(nf.id);
            if (existeDevolucaoVenda != null)
            {
               var devolucaoVendaBusiness = new DevolucaoVendaBusiness();
               devolucaoVendaBusiness.AtualizarStatusDevolucaoVenda(existeDevolucaoVenda.idDevolucaoVenda, EnumStatusDevolucao.Concluida);
               var devolucaoVenda = devolucaoVendaBusiness.BuscarPorId(existeDevolucaoVenda.idDevolucaoVenda);
               if (devolucaoVenda != null)
                  devolucaoVendaBusiness.CriarMovimentacaoFinanceiroEstoque(devolucaoVenda);
            }
            transacao.Complete();
         }
      }

      private int StringToUTF8ByteArray(object objString)
      {
         throw new NotImplementedException();
      }
      #endregion

      #region InutilizarComSoap
      public void InutilizarSoap(int serie, int numeroInicial, int numeroFinal, string justificativa)
      {
         this.parametro = new ParametroBusiness().BuscarParametroVigente();
         if (parametro.tb_nf_configuracao.NFeProducao)
            ambiente = TipoAmbiente.Producao;
         var empresa = new EmpresaBusiness().BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);
         //normal
         // var estado = (TCodUfIBGE)Enum.Parse(typeof(TCodUfIBGE), empresa.tb_cidade.tb_estado.Sigla, true);

         configuracaoNF = parametro.tb_nf_configuracao;
         if (string.IsNullOrEmpty(parametro.CertificadoDigital))
            throw new BusinessException("Não foi possível realizar a inutilização pois o Certificado Digital não foi configurado.");
         var certificadoDigital = new Certificado().Carregar(parametro.CertificadoDigital);

         if (string.IsNullOrEmpty(configuracaoNF.CaminhoArquivo) || !new DirectoryInfo(configuracaoNF.CaminhoArquivo).Exists)
            throw new BusinessException("Não foi possível gerar o XML da NFe pois o caminho configurado no parâmetro não existe.");


         var listaNotas = BuscarNotasPorNumero(numeroInicial, numeroFinal, serie);
         if (listaNotas.Where(x => x.idNFStatus != (int)EnumStatusNFe.AguardandoEnvio && x.idNFStatus != (int)EnumStatusNFe.CanceladaSemEmissao && x.idNFStatus != (int)EnumStatusNFe.ErroEmissaoNFe).ToList().Count > 0)
            throw new BusinessException("Não poderá ser Inutilizada uma nota com os seguintes Status:\n" +
               "Emitida Com Sucesso, Cancelada, Inutilizada, Erro no Cancelamento ou Denegada.\n" +
               "Favor informar um intervalo de notas que não esteja com uma das situações demonstradas acima.");

         //criar pasta no diretório selecionado no parametro
         var nomePasta = (DateTime.Now.Year + "-" + DateTime.Now.Month.ToString().PadLeft(2, '0')).ToString();
         if (!Directory.Exists(configuracaoNF.CaminhoArquivo + "\\" + nomePasta))
            Directory.CreateDirectory(configuracaoNF.CaminhoArquivo + "\\" + nomePasta);

         //iniciar dados necessários para a inutilizaçao
         _configuracoes = new ConfiguracaoApp();
         _configuracoes.CfgServico.TimeOut = 30000;
         _configuracoes.CfgServico.cUF = (Estado)CodigoEstado(empresa);
         _configuracoes.CfgServico.VersaoNfeInutilizacao = VersaoServico.ve400;
         _configuracoes.CfgServico.SalvarXmlServicos = true;
         _configuracoes.CfgServico.DiretorioSalvarXml = configuracaoNF.CaminhoArquivo + "\\" + nomePasta;

         var DiretorioRaiz = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

         _configuracoes.CfgServico.DiretorioSchemas = DiretorioRaiz + "\\PCInfo\\Schemas";
         _configuracoes.CfgServico.ProtocoloDeSeguranca = SecurityProtocolType.Tls12;
         if (ambiente == TipoAmbiente.Producao)
            _configuracoes.CfgServico.tpAmb = TipoAmbiente.Producao;
         else
            _configuracoes.CfgServico.tpAmb = TipoAmbiente.Homologacao;

         ServicosNFe servicosNfe = new ServicosNFe(_configuracoes.CfgServico, certificadoDigital);

         var cnpj = RemoveCaracteresEspeciaisNFe(empresa.NumeroInscricao);
         string motivo = StringUtis.SubstituirCarecteresAcentuados(RemoveCaracteresEspeciaisNFe(justificativa));

         var retornoConsulta = servicosNfe.NfeInutilizacao(cnpj, DateTime.Now.Year.ToString().Remove(0, 1).ToInt32(), 55, serie, numeroInicial, numeroFinal, motivo);

         if (retornoConsulta != null && retornoConsulta.Retorno.infInut.nProt !=null)
         {
            MessageBoxUtils.ExibeMensagem("Inutilização de número homologado");

            //Atualiza o status 
            foreach (var nota in listaNotas)
               Cancelar4_0(nota, EnumStatusNFe.Inutilizada, justificativa, true);
         }
         else
            throw new BusinessException(retornoConsulta.Retorno.infInut.xMotivo);
      }
      #endregion

      private List<tb_nfe> BuscarNotasPorNumero(int numeroInicial, int numeroFinal, int serie)
      {
         return new NFeRepository().BuscarNotasPorNumero(numeroInicial, numeroFinal, serie);
      }

      public tb_nfe BuscarPorIdSemInclude(int idNFe)
      {
         return new NFeRepository().BuscarPorIdSemInclude(idNFe);
      }

      private void AtualizarConfiguracao(int numeroNota)
      {
         if (numeroNota > configuracaoNF.UltimoNumeroNota)
         {
            configuracaoNF.UltimoNumeroNota = numeroNota;
            new NFConfiguracaoBusiness().Atualizar(configuracaoNF);
         }
      }
      public decimal BuscarAliquotaCSOSN(tb_produto_imposto produtoImposto, bool ignorarCalcularAliquotaNormalICMS)
      {
         if (!ignorarCalcularAliquotaNormalICMS)
            if (produtoImposto.CalcularAliquotaNormalICMS)
               return produtoImposto.AliquotaICMS.GetValueOrDefault();

         var receitaBruta = new ReceitaBrutaAcumuladaBusiness().BuscarUltimoMesApurado();
         if (receitaBruta != null)
            valorAcumulado = receitaBruta.ValorAcumulado;

         //Comercio Simples Nacional
         if (produtoImposto.idTabela >= 1 && produtoImposto.idTabela <= 9)
         {
            return new ComercioSimplesNacionalBusiness().BuscarFaixaPorValor(valorAcumulado).ICMS;
         }

         //Industria Simples Nacional
         else if (produtoImposto.idTabela >= 10 && produtoImposto.idTabela <= 32)
         {
            return new IndustriaSimplesNacionalBusiness().BuscarFaixaPorValor(valorAcumulado).ICMS;
         }

         //Serviço Simples Nacional 
         else if (produtoImposto.idTabela >= 33 && produtoImposto.idTabela <= 57)
         {
            return new ServicoSimplesNacionalBusiness().BuscarFaixaPorValor(valorAcumulado).Aliquota;
         }

         return 0;
      }

      #region AtualizarNFe 4.0
      public void AtualizarNFe4_0(tb_nfe nf, infNFe nfe, string xml)
      {
         this.parametro = new ParametroBusiness().BuscarParametroVigente();
         configuracaoNF = parametro.tb_nf_configuracao;
         if (nfe != null)
         {
            nf.NumeroSerie = nfe.ide.serie.ToInt32();
            nf.Chave = nfe.Id.Replace("NFe", string.Empty);
            nf.NumeroNota = nfe.ide.nNF.ToInt32();
            if (nf.TipoEmissao.In((int)TipoEmissao.teFSDA, (int)TipoEmissao.teFSIA))
               nf.ChaveFS = GerarChaveFS4_0(nfe);
         }
         else
            nf.Chave = string.Empty;

         nf.XML = xml;
         nf.DataEnvio = DateTime.Now;

         Salvar(nf);
      }
      #endregion

      public RetornoNfeConsultaProtocolo ConsultaChave(tb_nfe nf)
      {
         this.parametro = new ParametroBusiness().BuscarParametroVigente();
         if (parametro.tb_nf_configuracao.NFeProducao)
            ambiente = TipoAmbiente.Producao;
         var empresaB = new EmpresaBusiness().BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);
         empresa = empresaB;
         X509Certificate2 certificadoDigital = null;
         configuracaoNF = parametro.tb_nf_configuracao;
         if (string.IsNullOrEmpty(parametro.CertificadoDigital))
            throw new BusinessException("Certificado Digital não foi configurado.");
         certificadoDigital = new Certificado().Carregar(parametro.CertificadoDigital);

         var nomePasta = (nf.DataEmissao.Year + "-" + nf.DataEmissao.Month.ToString().PadLeft(2, '0')).ToString();

         _configuracoes = new ConfiguracaoApp();
         _configuracoes.CfgServico.TimeOut = 3000;
         _configuracoes.CfgServico.cUF = (Estado)CodigoEstado(empresa);
         _configuracoes.CfgServico.Certificado.Serial = certificadoDigital.SerialNumber;
         _configuracoes.CfgServico.SalvarXmlServicos = true;
         _configuracoes.CfgServico.ProtocoloDeSeguranca = SecurityProtocolType.Tls12;
         _configuracoes.CfgServico.tpAmb = TipoAmbiente.Producao;
         _configuracoes.CfgServico.ModeloDocumento = ModeloDocumento.NFe;
         var DiretorioRaiz = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
         _configuracoes.CfgServico.DiretorioSchemas = DiretorioRaiz + "\\PCInfo\\Schemas";
         _configuracoes.CfgServico.DiretorioSalvarXml = configuracaoNF.CaminhoArquivo + "\\" + nomePasta;

         var servicoNFe = new ServicosNFe(_configuracoes.CfgServico);
         var retornoConsulta = servicoNFe.NfeConsultaProtocolo(nf.Chave);

         if (retornoConsulta.Retorno.protNFe != null)
         {
             nfe = new NFeBusiness().BuscarPorId(nf.id);
            _nfe = ObterNfeValidada(_configuracoes.CfgServico.VersaoNFeAutorizacao = VersaoServico.ve400, _configuracoes.CfgServico.ModeloDocumento, Convert.ToInt32(nfe.idLote), _configuracoes.ConfiguracaoCsc, _configuracoes.CfgServico);

            var nfeproc = new nfeProc
            {
               NFe = _nfe,
               protNFe = retornoConsulta.Retorno.protNFe,
               versao = retornoConsulta.Retorno.versao
            };
            if (nfeproc.protNFe != null)
            {
               var novoArquivo = _configuracoes.CfgServico.DiretorioSalvarXml + @"\" + nfeproc.protNFe.infProt.chNFe +
                                 "-procNfe.xml";
               FuncoesXml.ClasseParaArquivoXml(nfeproc, novoArquivo);

            }
         }

         return retornoConsulta;

      }
      public void AtualizarProtocoloDigestValue(int id, string numeroProtocoloAutorizacaoUso, string digestValue)
      {
         new NFeRepository().AtualizarProtocoloDigestValue(id, numeroProtocoloAutorizacaoUso, digestValue);
      }

      private void ValidarCampos(tb_nfe nfe)
      {
         var mensagem = new StringBuilder();
         if (!configuracaoNF.NumeroSerie.HasValue)
            mensagem.AppendFormat("É necessário informar a série da NFe nas configurações do Parâmetro.");
         if (nfe.TipoNota != EnumTipoNota.DevolucaoCompra && nfe.TipoNota != EnumTipoNota.DevolucaoVenda)
         {
            foreach (var item in nfe.tb_nfe_produto)
            {
               item.tb_produto = new ProdutoBusiness().BuscarPorId(item.idProduto);
               if (!item.idCFOP.HasValue)
                  mensagem.AppendFormat("É necessário informar o CFOP para o produto: {0}.\r", item.tb_produto.Descricao);
               if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
               {
                  if (!item.idCSOSN.HasValue)
                     mensagem.AppendFormat("É necessário informar o CSOSN para o produto: {0}.\r", item.tb_produto.Descricao);
               }
               else
                 if (!item.idCSTICMS.HasValue)
                  mensagem.AppendFormat("É necessário informar o CST ICMS para o produto: {0}.\r", item.tb_produto.Descricao);

               if (string.IsNullOrEmpty(item.tb_produto.NCM))
                  mensagem.AppendFormat("É necessário informar o NCM para o produto: {0}.\r", item.tb_produto.Descricao);

               if (!string.IsNullOrEmpty(item.tb_produto.NCM) && item.tb_produto.NCM.Length != 8)
                  mensagem.AppendFormat("NCM deve possuir 8 digitos para o produto: {0}.\r", item.tb_produto.Descricao);
            }
         }
         if (mensagem.Length > 0)
            throw new BusinessException(mensagem.ToString());
      }

      public tb_nfe DuplicarNota(tb_nfe nfe)
      {
         int idNota;
         using (var transacao = new TransactionScope())
         {
            nfe = BuscarPorId(nfe.id);
            this.parametro = new ParametroBusiness().BuscarParametroVigente();
            var novaNota = new tb_nfe();
            novaNota.idEmpresa = nfe.idEmpresa;
            novaNota.Chave = string.Empty;
            novaNota.FormaPagamento = 0; // A vista
            novaNota.NumeroNota = (parametro.tb_nf_configuracao.UltimoNumeroNota + 1);
            novaNota.idNFStatus = (int)EnumStatusNFe.AguardandoEnvio;
            novaNota.idCliente = nfe.idCliente;
            novaNota.DataEmissao = DateTime.Now;
            novaNota.DataEntradaSaida = DateTime.Now;
            novaNota.TipoEmissao = nfe.TipoEmissao;
            novaNota.CodigoFinalidade = nfe.CodigoFinalidade;
            novaNota.TipoNota = nfe.TipoNota;
            novaNota.Valor = nfe.Valor;
            novaNota.InformacoesComplementares = nfe.InformacoesComplementares;
            novaNota.InformacoesEditaveis = nfe.InformacoesEditaveis;
            novaNota.InformacoesFisco = nfe.InformacoesFisco;
            novaNota.NaturezaOperacao = nfe.NaturezaOperacao;
            novaNota.NumeroSerie = nfe.NumeroSerie;
            novaNota.DataCadastro = DateTime.Now;
            novaNota.Ambiente = nfe.Ambiente;
            var numeroNota = new NFeBusiness().BuscarNotasPorNumero(novaNota.NumeroNota.ToInt32(), novaNota.NumeroNota.ToInt32(), novaNota.NumeroSerie.ToInt32());
            if (numeroNota.Count > 0)
               throw new BusinessException("Numero de Nota: " + novaNota.NumeroNota + " e série: " + novaNota.NumeroSerie + " existente no banco de dados!");
            novaNota.ChaveReferencia = string.Empty;
            novaNota.tb_nfe_referencia.Clear();
            novaNota.tb_nfe_referencia_cupom.Clear();

            if (nfe.tb_nfe_produto != null && nfe.tb_nfe_produto.Count > 0)
            {
               var listaProdutos = new List<tb_nfe_produto>();
               foreach (var produto in nfe.tb_nfe_produto)
               {
                  var produtoNovo = new tb_nfe_produto();
                  produtoNovo.idNFe = novaNota.id;
                  produtoNovo.idProduto = produto.idProduto;
                  produtoNovo.idOrigemMercadoria = produto.idOrigemMercadoria;
                  produtoNovo.idCSTICMS = produto.idCSTICMS;
                  produtoNovo.idCSOSN = produto.idCSOSN;
                  produtoNovo.idCFOP = produto.idCFOP;
                  produtoNovo.idCSTPIS = produto.idCSTPIS;
                  produtoNovo.idCSTCOFINS = produto.idCSTCOFINS;
                  produtoNovo.idCSTIPI = produto.idCSTIPI;
                  produtoNovo.idPartilhaICMS = produto.idPartilhaICMS;
                  produtoNovo.Quantidade = produto.Quantidade;
                  produtoNovo.ValorUnitario = produto.ValorUnitario;
                  produtoNovo.ValorBaseCalculoIPI = produto.ValorBaseCalculoIPI;
                  produtoNovo.ValorAliquotaIPI = produto.ValorAliquotaIPI;
                  produtoNovo.ValorIPI = produto.ValorIPI;
                  produtoNovo.ValorFrete = produto.ValorFrete;
                  produtoNovo.ValorSeguro = produto.ValorSeguro;
                  produtoNovo.ValorDesconto = produto.ValorDesconto;
                  produtoNovo.ValorOutrasDespesas = produto.ValorOutrasDespesas;
                  produtoNovo.ValorBaseCalculoICMS = produto.ValorBaseCalculoICMS;
                  produtoNovo.ValorAliquotaICMS = produto.ValorAliquotaICMS;
                  produtoNovo.ValorICMS = produto.ValorICMS;
                  produtoNovo.PorcentagemDiferimento = produto.PorcentagemDiferimento;
                  produtoNovo.ValorBaseCalculoICMSST = produto.ValorBaseCalculoICMSST;
                  produtoNovo.ValorICMSST = produto.ValorICMSST;
                  produtoNovo.ValorBaseCalculoPIS = produto.ValorBaseCalculoPIS;
                  produtoNovo.ValorAliquotaPIS = produto.ValorAliquotaPIS;
                  produtoNovo.ValorPIS = produto.ValorPIS;
                  produtoNovo.ValorBaseCalculoCOFINS = produto.ValorBaseCalculoCOFINS;
                  produtoNovo.ValorAliquotaCOFINS = produto.ValorAliquotaCOFINS;
                  produtoNovo.ValorCOFINS = produto.ValorCOFINS;
                  produtoNovo.ValorAliquotaST = produto.ValorAliquotaST;
                  produtoNovo.MVA = produto.MVA;
                  produtoNovo.PorcentagemReducao = produto.PorcentagemReducao;
                  produtoNovo.PorcentagemReducaoST = produto.PorcentagemReducaoST;
                  produtoNovo.ComplementoDescricao = produto.ComplementoDescricao;
                  produtoNovo.EntregueFlag = produto.EntregueFlag;
                  produtoNovo.PorcentagemFCP = produto.PorcentagemFCP;
                  produtoNovo.ValorFCP = produto.ValorFCP;
                  produtoNovo.PorcentagemIntraestadual = produto.PorcentagemIntraestadual;
                  produtoNovo.ValorIntraestadual = produto.ValorIntraestadual;
                  produtoNovo.PorcentagemInterestadual = produto.PorcentagemInterestadual;
                  produtoNovo.ValorInterestadual = produto.ValorInterestadual;
                  produtoNovo.AliquotaCreditoICMS = produto.AliquotaCreditoICMS;
                  produtoNovo.ValorCreditoICMS = produto.ValorCreditoICMS;
                  produtoNovo.ValorTotalProduto = produto.ValorTotalProduto;
                  listaProdutos.Add(produtoNovo);
               }
               novaNota.tb_nfe_produto = listaProdutos;
            }

            if (nfe.tb_nfe_frete != null)
            {
               novaNota.tb_nfe_frete = new tb_nfe_frete();
               novaNota.tb_nfe_frete.idNFe = novaNota.id;
               novaNota.tb_nfe_frete.idTransportadora = nfe.tb_nfe_frete.idTransportadora;
               novaNota.tb_nfe_frete.idVeiculo = nfe.tb_nfe_frete.idVeiculo;
               novaNota.tb_nfe_frete.idMotorista = nfe.tb_nfe_frete.idMotorista;
               novaNota.tb_nfe_frete.idTipoFrete = nfe.tb_nfe_frete.idTipoFrete;
               novaNota.tb_nfe_frete.EspecieVolumes = nfe.tb_nfe_frete.EspecieVolumes;
               novaNota.tb_nfe_frete.Marca = nfe.tb_nfe_frete.Marca;
               novaNota.tb_nfe_frete.Numeracao = nfe.tb_nfe_frete.Numeracao;
               novaNota.tb_nfe_frete.PesoLiquido = nfe.tb_nfe_frete.PesoLiquido;
               novaNota.tb_nfe_frete.PesoBruto = nfe.tb_nfe_frete.PesoBruto;
               novaNota.tb_nfe_frete.ValorFrete = nfe.tb_nfe_frete.ValorFrete;
               novaNota.tb_nfe_frete.QuantidadeVolumes = nfe.tb_nfe_frete.QuantidadeVolumes;
            }

            new NFeRepository().Salvar(novaNota);
            idNota = novaNota.id;
            parametro.tb_nf_configuracao.UltimoNumeroNota = novaNota.NumeroNota.ToInt32();
            new NFConfiguracaoBusiness().Atualizar(parametro.tb_nf_configuracao);
            var nota = BuscarPorId(idNota);
            //Gerar e Salvar o XML da nota
            GerarNFe4_0(nota, false);
            transacao.Complete();
         }
         return BuscarPorId(idNota);
      }

      private XmlNode GetXmlNode(XElement element)
      {
         using (XmlReader xmlReader = element.CreateReader())
         {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlReader);
            return xmlDoc;
         }
      }
      public List<tb_nfe> BuscarNotasModeloDoisPorPeriodo(DateTime dataInicial, DateTime dataFinal)
      {
         return new NFeRepository().BuscarNotasModeloDoisPorPeriodo(dataInicial, dataFinal);
      }

      public List<tb_nfe> BuscarNotasCanceladasPorPeriodo(DateTime dataInicial, DateTime dataFinal)
      {
         return new NFeRepository().BuscarNotasCanceladasPorPeriodo(dataInicial, dataFinal);
      }
      private int CodigoEstado(tb_empresa empresa)
      {
         int codEstado = 0;
         if (empresa.UF == "RO")
            codEstado = 11;
         else if (empresa.UF == "AC")
            codEstado = 12;
         else if (empresa.UF == "AM")
            codEstado = 13;
         else if (empresa.UF == "RR")
            codEstado = 14;
         else if (empresa.UF == "PA")
            codEstado = 15;
         else if (empresa.UF == "AP")
            codEstado = 16;
         else if (empresa.UF == "TO")
            codEstado = 17;
         else if (empresa.UF == "MA")
            codEstado = 21;
         else if (empresa.UF == "PI")
            codEstado = 22;
         else if (empresa.UF == "CE")
            codEstado = 23;
         else if (empresa.UF == "RN")
            codEstado = 24;
         else if (empresa.UF == "PB")
            codEstado = 25;
         else if (empresa.UF == "PE")
            codEstado = 26;
         else if (empresa.UF == "AL")
            codEstado = 26;
         else if (empresa.UF == "SE")
            codEstado = 28;
         else if (empresa.UF == "BA")
            codEstado = 29;
         else if (empresa.UF == "MG")
            codEstado = 31;
         else if (empresa.UF == "ES")
            codEstado = 32;
         else if (empresa.UF == "RJ")
            codEstado = 33;
         else if (empresa.UF == "SP")
            codEstado = 35;
         else if (empresa.UF == "PR")
            codEstado = 41;
         else if (empresa.UF == "SC")
            codEstado = 42;
         else if (empresa.UF == "RS")
            codEstado = 43;
         else if (empresa.UF == "MS")
            codEstado = 50;
         else if (empresa.UF == "MT")
            codEstado = 51;
         else if (empresa.UF == "GO")
            codEstado = 52;
         else if (empresa.UF == "DF")
            codEstado = 53;
         else if (empresa.UF == "EX")
            codEstado = 99;
         return codEstado;
      }
      public string ConfigurarMensagemPadrao(string mensagemPadraoNFe, tb_nfe nfe)
      {
         //Criar opção no parâmetro para informar chave da nota, série da nota, número da nota, valor da nota
         var cliente = new ClienteBusiness().BuscarPorId(nfe.idCliente);
         return mensagemPadraoNFe
           .Replace("<<ChaveNota>>", nfe.Chave.ToString())
           .Replace("<<SerieNota>>", nfe.NumeroSerie.ToString())
           .Replace("<<NumeroNota>>", nfe.NumeroNota.ToString())
           .Replace("<<ValorNota>>", nfe.ValorTotalNota.GetValueOrDefault().ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO))
           .Replace("<<NomeCliente>>", cliente.Nome)
           .Replace("\r\n", "<br />");
      }

      private void GerarChave4_0(infNFe _informacoes, tb_nfe nfee, string xml)
      {
         this._ChaveFiscal = new ChaveFiscal();

         var dadosChave = ChaveFiscal.ObterChave(_informacoes.ide.cUF, _informacoes.ide.dhEmi.DateTime, _informacoes.emit.CNPJ,
        ModeloDocumento.NFe, _informacoes.ide.serie, _informacoes.ide.nNF, _informacoes.ide.tpEmis.ToInt32(), _informacoes.ide.cNF.ToInt32());
         _informacoes.Id = "NFe" + dadosChave.Chave.Substring(0, 44);
         _informacoes.ide.cDV = Convert.ToInt16(dadosChave.DigitoVerificador);
         nfee.Chave = dadosChave.Chave.Substring(0, 44);

         nfee.XML = xml;
         nfee.DataEnvio = DateTime.Now;

         Salvar(nfee);
      }

      private string GerarChaveFS4_0(infNFe infNfe)
      {
         var chaveNFeFS = new ChaveNFeFS();
         chaveNFeFS.cUF = CodigoEstado(empresa).ToString();
         chaveNFeFS.tpEmis = TipoEmissao.teFSDA.ToInt32().ToString();
         chaveNFeFS.CNPJ = infNfe.emit.CNPJ;
         chaveNFeFS.vNF = infNfe.total.ICMSTot.vNF.ToString();
         if (infNfe.total.ICMSTot.vICMS > 0)
            chaveNFeFS.ICMSp = "1"; // há destaque de ICMS próprio;
         else
            chaveNFeFS.ICMSp = "2"; // não há destaque de ICMS próprio;
         if (infNfe.total.ICMSTot.vST > 0)
            chaveNFeFS.ICMSs = "1"; // há destaque de ICMS por substituição tributária;
         else
            chaveNFeFS.ICMSs = "2"; // não há destaque de ICMS por substituição tributária;
         chaveNFeFS.DD = nfe.DataEmissao.ToString("dd");
         var chaveGerada = chaveNFeFS.Gerar();
         return chaveGerada;

      }

      public List<tb_nfe> BuscarPorPeriodo(DateTime dataInicial, DateTime dataFinal)
      {
         return new NFeRepository().BuscarPorPeriodo(dataInicial, dataFinal);
      }

      private decimal valorBaseCreditoICMS = 0;

      #region Cancelamento 4.0
      public bool Cancelar4_0(tb_nfe nf, EnumStatusNFe status, string motivo, bool validaStatus)
      {
         if (nf.idNFStatus == (int)EnumStatusNFe.EmitidaComSucesso)
         {
            parametro = new ParametroBusiness().BuscarParametroVigente();
            configuracaoNF = parametro.tb_nf_configuracao;
            if (!Directory.Exists(configuracaoNF.CaminhoArquivo))
               throw new BusinessException("Caminho configurado no parâmetro inexistente.");

            var empresa = new EmpresaBusiness().BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);

            var nomePasta = (nf.DataEmissao.Year + "-" + nf.DataEmissao.Month.ToString().PadLeft(2, '0')).ToString();

            X509Certificate2 certificadoDigital = null;
            if (!string.IsNullOrEmpty(parametro.CertificadoDigital))
            {
               certificadoDigital = new Certificado().Carregar(parametro.CertificadoDigital);
               if (certificadoDigital != null)
                  ValidarValidadeCertificado(certificadoDigital);
            }
            else if (EnviarMensagem != null)
               throw new BusinessException("O Certificado Digital não foi configurado.");

            _configuracoes = new ConfiguracaoApp();
            _configuracoes.CfgServico.ProtocoloDeSeguranca = SecurityProtocolType.Tls12;
            _configuracoes.CfgServico.TimeOut = 6000;
            _configuracoes.CfgServico.cUF = (Estado)CodigoEstado(empresa);
            _configuracoes.CfgServico.Certificado.Serial = certificadoDigital.SerialNumber;
            _configuracoes.CfgServico.VersaoRecepcaoEventoCceCancelamento = VersaoServico.ve400;
            _configuracoes.CfgServico.SalvarXmlServicos = true;
            _configuracoes.CfgServico.DiretorioSalvarXml = configuracaoNF.CaminhoArquivo + "\\" + nomePasta;

            var DiretorioRaiz = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            _configuracoes.CfgServico.DiretorioSchemas = DiretorioRaiz + "\\PCInfo\\Schemas";
            ambiente = (TipoAmbiente)nf.Ambiente;
            if (ambiente == TipoAmbiente.Producao)
               _configuracoes.CfgServico.tpAmb = TipoAmbiente.Producao;
            else
               _configuracoes.CfgServico.tpAmb = TipoAmbiente.Homologacao;

            ServicosNFe servicosNfe = new ServicosNFe(_configuracoes.CfgServico);

            var cpfcnpj = empresa.NumeroInscricao.Trim();

            Network net = new Network();
            if (net.IsAvailable == false)
            {
               throw new BusinessException("Sem Conexão Com a Internet!");
            }

            var retornoCancelamento = servicosNfe.RecepcaoEventoCancelamento(Convert.ToInt32(nf.idLote),
                    Convert.ToInt16(1), nf.NumeroProtocolo, nf.Chave, motivo, cpfcnpj);

            if (retornoCancelamento.Retorno.xMotivo.Contains("não foi possível criar um canal seguro para ssl/tls"))
               throw new BusinessException("Por favor verifique se o certificado é válido.");

            var statusRetorno = retornoCancelamento.Retorno.cStat;

            if (retornoCancelamento.Retorno.xMotivo.Contains("registro de passagem"))
               throw new BusinessException(retornoCancelamento.Retorno.xMotivo);
            else if (retornoCancelamento.Retorno.xMotivo.ToLower().Contains("falha"))
               throw new BusinessException("Falha no cancelamento. Falha na estrutura do XML. Consulte o suporte Técnico. " + System.Environment.NewLine + retornoCancelamento.Retorno.xMotivo);
            else if (retornoCancelamento.Retorno.xMotivo.ToLower().Contains("rejeicao") && !retornoCancelamento.Retorno.xMotivo.ToLower().Contains("rejeicao: duplicidade de evento"))
               throw new BusinessException("Rejeição no cancelamento da NFe. Consulte o XML de Retorno ou suporte Técnico." + System.Environment.NewLine + System.Environment.NewLine + "Status: " + statusRetorno + System.Environment.NewLine + "Motivo: " + retornoCancelamento.Retorno.xMotivo);
            else if (retornoCancelamento.ProcEventosNFe.FirstOrDefault().retEvento.infEvento.nProt == null)
               throw new BusinessException("Rejeição no cancelamento da NFe. Consulte o XML de Retorno ou suporte Técnico.\n" +
                  retornoCancelamento.ProcEventosNFe.FirstOrDefault().retEvento.infEvento.xMotivo);

         }

         AtualizarStatusMotivo4_0(nf.id, status, motivo, validaStatus);

         if (nf.idNFStatus != (int)EnumStatusNFe.EmitidaComSucesso)
            if (status != EnumStatusNFe.Inutilizada && MessageBoxUtils.ExibeMensagemQuestion(string.Format("Deseja inutilizar o número {0}?", nf.NumeroNota)))
               InutilizarSoap(nf.NumeroSerie.ToInt32(), nf.NumeroNota.ToInt32(), nf.NumeroNota.ToInt32(), "Nota fiscal nao transmitida pelo emissor.");

         var nfe = BuscarPorId(nf.id);
         if (nfe != null)
         {
            var existeInutilizacao = new InutilizacaoNFeBusiness().BuscarPorNumeroNota(nfe.NumeroNota);
            if (existeInutilizacao != null)
               nf.idNFStatus = (int)status;
         }

         return true;
      }

      public void AtualizarStatusMotivo4_0(int idNFe, EnumStatusNFe status, string motivo, bool validaStatus)
      {
         using (var transacao = new TransactionScope())
         {
            var vendaBusiness = new VendaBusiness();
            var vendaNFeBusiness = new VendaNFeBusiness();

            //Colocar para devoluções
            if (validaStatus == true)
               new NFeRepository().AtualizarStatus(idNFe, status, motivo);

            if (status == EnumStatusNFe.EmitidaComSucesso)
            {
               var nfe = new NFeBusiness().BuscarPorId(idNFe);
               var vendaNFe = vendaNFeBusiness.BuscarPorIdNFe(idNFe);
               if (vendaNFe != null)
               {
                  var venda = vendaBusiness.BuscarPorId(vendaNFe.idVenda);
                  if (venda != null)
                     vendaBusiness.AtualizarStatusVenda(venda.id, EnumStatusVenda.Concluida);
               }
               else
               {
                  new NFeRepository().AtualizarStatus(idNFe, status, "");
               }
               var existeCompra = new CompraNFeBusiness().BuscarPorIdNFe(idNFe);
               if (existeCompra != null)
               {
                  new CompraBusiness().AtualizarStatusCompra(existeCompra.idCompra, EnumStatusVenda.Concluida);
                  new CompraNFBusiness().AtualizarDadosNFe(existeCompra.idCompra, BuscarPorId(idNFe));
               }

               var existeDevolucaoCompra = new DevolucaoCompraNFeBusiness().BuscarPorIdNFe(idNFe);
               if (existeDevolucaoCompra != null)
               {
                  var devolucaoCompraBusiness = new DevolucaoCompraBusiness();
                  devolucaoCompraBusiness.AtualizarStatusDevolucaoCompra(existeDevolucaoCompra.idDevolucaoCompra, EnumStatusDevolucao.Concluida);
                  var devolucaoCompra = devolucaoCompraBusiness.BuscarPorId(existeDevolucaoCompra.idDevolucaoCompra);
                  if (devolucaoCompra != null)
                     devolucaoCompraBusiness.CriarMovimentacaoFinanceiroEstoque(devolucaoCompra);
               }

               var existeDevolucaoVenda = new DevolucaoVendaNFeBusiness().BuscarPorIdNFe(idNFe);
               if (existeDevolucaoVenda != null)
               {
                  var devolucaoVendaBusiness = new DevolucaoVendaBusiness();
                  devolucaoVendaBusiness.AtualizarStatusDevolucaoVenda(existeDevolucaoVenda.idDevolucaoVenda, EnumStatusDevolucao.Concluida);
                  var devolucaoVenda = devolucaoVendaBusiness.BuscarPorId(existeDevolucaoVenda.idDevolucaoVenda);
                  if (devolucaoVenda != null)
                     devolucaoVendaBusiness.CriarMovimentacaoFinanceiroEstoque(devolucaoVenda);
               }

            }
            else if (status == EnumStatusNFe.Cancelada || status == EnumStatusNFe.CanceladaSemEmissao)
            {
               var vendaNFe = vendaNFeBusiness.BuscarPorIdNFe(idNFe);
               if (vendaNFe != null)
               {
                  var venda = vendaBusiness.BuscarPorId(vendaNFe.idVenda);
                  if (venda != null)
                     vendaBusiness.Cancelar(venda, motivo);
               }
               var existeCompra = new CompraNFeBusiness().BuscarPorIdNFe(idNFe);
               if (existeCompra != null)
                  new CompraBusiness().AtualizarStatusCompra(existeCompra.idCompra, EnumStatusVenda.Cancelada);


               var existeDevolucaoCompra = new DevolucaoCompraNFeBusiness().BuscarPorIdNFe(idNFe);
               if (existeDevolucaoCompra != null)
               {
                  var devolucaoCompraBusiness = new DevolucaoCompraBusiness();
                  var devolucaoCompra = devolucaoCompraBusiness.BuscarPorId(existeDevolucaoCompra.idDevolucaoCompra);
                  devolucaoCompraBusiness.Cancelar(devolucaoCompra, motivo);

               }

               var existeDevolucaoVenda = new DevolucaoVendaNFeBusiness().BuscarPorIdNFe(idNFe);
               if (existeDevolucaoVenda != null)
               {
                  var devolucaoVendaBusiness = new DevolucaoVendaBusiness();
                  var devolucaoVenda = devolucaoVendaBusiness.BuscarPorId(existeDevolucaoVenda.idDevolucaoVenda);
                  if (devolucaoVenda != null)
                     devolucaoVendaBusiness.Cancelar(devolucaoVenda, motivo);
               }
            }
            else if (status == EnumStatusNFe.AguardandoEnvio)
            {
               var vendaNFe = vendaNFeBusiness.BuscarPorIdNFe(idNFe);
               if (vendaNFe != null)
               {
                  var venda = vendaBusiness.BuscarPorId(vendaNFe.idVenda);
                  if (venda != null)
                     vendaBusiness.AtualizarStatusVenda(venda.id, EnumStatusVenda.AguardandoEnvio);
               }
            }

            transacao.Complete();
         }
      }

      #endregion

      #region Carta de Correção 4.0
      public bool EnviarCartaCorrecao4_0(tb_nfe nf)
      {
         parametro = new ParametroBusiness().BuscarParametroVigente();
         configuracaoNF = parametro.tb_nf_configuracao;
         if (!Directory.Exists(configuracaoNF.CaminhoArquivo))
            throw new BusinessException("Caminho configurado no parâmetro inexistente.");

         var empresa = new EmpresaBusiness().BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);

         var nomePasta = (nf.DataEmissao.Year + "-" + nf.DataEmissao.Month.ToString().PadLeft(2, '0')).ToString();
         X509Certificate2 certificadoDigital = null;
         certificadoDigital = new Certificado().Carregar(parametro.CertificadoDigital);
         if (!string.IsNullOrEmpty(parametro.CertificadoDigital))
         {
            certificadoDigital = new Certificado().Carregar(parametro.CertificadoDigital);
            if (certificadoDigital != null)
               ValidarValidadeCertificado(certificadoDigital);
         }
         else if (EnviarMensagem != null)
            throw new BusinessException("O Certificado Digital não foi configurado.");

         _configuracoes = new ConfiguracaoApp();
         _configuracoes.CfgServico.TimeOut = 6000;
         _configuracoes.CfgServico.cUF = (Estado)CodigoEstado(empresa);
         _configuracoes.CfgServico.Certificado.Serial = certificadoDigital.SerialNumber;
         _configuracoes.CfgServico.VersaoRecepcaoEventoCceCancelamento = VersaoServico.ve400;
         _configuracoes.CfgServico.SalvarXmlServicos = true;
         _configuracoes.CfgServico.DiretorioSalvarXml = configuracaoNF.CaminhoArquivo + "\\" + nomePasta;

         var DiretorioRaiz = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

         _configuracoes.CfgServico.DiretorioSchemas = DiretorioRaiz + "\\PCInfo\\Schemas";
         _configuracoes.CfgServico.ProtocoloDeSeguranca = SecurityProtocolType.Tls12;
         ambiente = (TipoAmbiente)nf.Ambiente;
         if (ambiente == TipoAmbiente.Producao)
            _configuracoes.CfgServico.tpAmb = TipoAmbiente.Producao;
         else
            _configuracoes.CfgServico.tpAmb = TipoAmbiente.Homologacao;

         ServicosNFe servicosNfe = new ServicosNFe(_configuracoes.CfgServico);

         var retornoCartaCorrecao = servicosNfe.RecepcaoEventoCartaCorrecao(nf.idLote.ToInt32(), 1, nf.Chave, nf.Motivo, empresa.NumeroInscricao.Trim());

         if (retornoCartaCorrecao.Retorno.xMotivo.Contains("não foi possível criar um canal seguro para ssl/tls"))
            throw new BusinessException("Por favor verifique se o certificado é válido.");
         if (retornoCartaCorrecao.Retorno.xMotivo.Contains("Falha"))
            throw new BusinessException("Falha no envio da Carta de Correção. Falha na estrutura do XML. Consulte o suporte Técnico.");
         else if (retornoCartaCorrecao.Retorno.xMotivo.Contains("rejeicao"))
            throw new BusinessException("Rejeição no envio da Carta de Correção. Consulte o XML de Retorno ou suporte Técnico.");


         return true;
      }
      #endregion

      #region Ciência da Operação
                                                                                                          
      public void ManifestacaoOperacao(TEventoTipo tipoEvento, tb_manifestacao_nfe nota)
      {
         this.parametro = new ParametroBusiness().BuscarParametroVigente();
         var eventoSoap = new PCInfo.Plus.Business.Classes.Soap.Manifestação.EventoSoap();
         var empresa = new EmpresaBusiness().BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);
         var estado = TCodUfIBGE.NACIONAL;
         eventoSoap.header.nfeCabecMsg.cUF = estado;
         eventoSoap.header.nfeCabecMsg.versaoDados = "1.00";

         var envEvento = eventoSoap.body.nfeDadosMsg.envEvento;
         envEvento.idLote = "1";
         var evento = envEvento.Evento;
         evento.InfEvento.Orgao = estado;
         evento.InfEvento.Ambiente = TAmb.Producao;
         evento.InfEvento.Chave = nota.Chave;
         if (empresa.TipoInscricao == (int)EnumTipoInscricao.CNPJ)
            evento.InfEvento.CNPJ = empresa.NumeroInscricao;
         else
            evento.InfEvento.CPF = empresa.NumeroInscricao;
         evento.InfEvento.DataHora = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
         evento.InfEvento.TipoEvento = tipoEvento;

         //Sequencial do evento para o mesmo tipo de evento. Para maioria dos eventos nSeqEvento=1, 
         //nos casos em que possa existir mais de um evento, como é o caso da Carta de Correção, 
         //o autor do evento deve numerar de forma sequencial.
         evento.InfEvento.Sequencial = 1;
         evento.InfEvento.Versao = "1.00";
         evento.InfEvento.DetalheEvento.Descricao = EnumHelper.GetEnumDescription(tipoEvento);

         if (tipoEvento == TEventoTipo.ConfirmacaoDaOperacao)
            evento.InfEvento.Id = "ID" + "210200" + evento.InfEvento.Chave + evento.InfEvento.Sequencial.ToString().PadLeft(2, '0');
         else if (tipoEvento == TEventoTipo.CienciaDaOperacao)
            evento.InfEvento.Id = "ID" + "210210" + evento.InfEvento.Chave + evento.InfEvento.Sequencial.ToString().PadLeft(2, '0');
         else if (tipoEvento == TEventoTipo.DesconhecimentoDaOperacao)
            evento.InfEvento.Id = "ID" + "210220" + evento.InfEvento.Chave + evento.InfEvento.Sequencial.ToString().PadLeft(2, '0');
         else if (tipoEvento == TEventoTipo.OperacaoNaoRealizada)
            evento.InfEvento.Id = "ID" + "210240" + evento.InfEvento.Chave + evento.InfEvento.Sequencial.ToString().PadLeft(2, '0');

         var xmlSoap = XmlUtils.Serializar<PCInfo.Plus.Business.Classes.Soap.Manifestação.EventoSoap>(eventoSoap);
         var xml = XmlUtils.Serializar<PCInfo.Plus.Business.Classes.Soap.Manifestação.Evento>(evento);
         xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "nfeCabecMsg", "xmlns", "http://www.portalfiscal.inf.br/nfe/wsdl/RecepcaoEvento");
         xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "nfeDadosMsg", "xmlns", "http://www.portalfiscal.inf.br/nfe/wsdl/RecepcaoEvento");
         xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "evento", "xmlns", "http://www.portalfiscal.inf.br/nfe");
         var certificadoDigital = new Certificado().Carregar(parametro.CertificadoDigital);
         PCInfo.Plus.Business.Classes.Soap.Manifestação.AssinaturaDigital assinatura = new PCInfo.Plus.Business.Classes.Soap.Manifestação.AssinaturaDigital();

         var docxml = new XmlDocument();
         docxml.LoadXml(xmlSoap);
         XmlNodeList nodeList = docxml.GetElementsByTagName("evento");

         string resposta = assinatura.Assinar(nodeList[0].OuterXml, certificadoDigital,
           string.Empty,
           evento.InfEvento.Id);

         nodeList[0].ParentNode.RemoveChild(nodeList[0]);
         var root = docxml.GetElementsByTagName("envEvento")[0];
         var xmlParse = XDocument.Parse(resposta);
         var novoNo = GetXmlNode(xmlParse.Root).ChildNodes[0];
         XmlNode newBook = docxml.ImportNode(novoNo, true);
         root.AppendChild(newBook);

         string xmlEvento = XElement.Parse(docxml.OuterXml).ToString(SaveOptions.DisableFormatting);
         configuracaoNF = parametro.tb_nf_configuracao;
         if (configuracaoNF.CaminhoDownload == null || !Directory.Exists(configuracaoNF.CaminhoDownload))
            throw new BusinessException("Selecione o caminho para download!");

         var caminhoDownload = configuracaoNF.CaminhoDownload.Insert(configuracaoNF.CaminhoDownload.Length, "\\Downloads");
         var caminhoEventos = configuracaoNF.CaminhoDownload.Insert(configuracaoNF.CaminhoDownload.Length, "\\Eventos");
         bool existePastaDownload = Directory.Exists(caminhoDownload);
         if (!existePastaDownload)
            Directory.CreateDirectory(caminhoDownload);

         bool existePastaEventos = Directory.Exists(caminhoEventos);
         if (!existePastaEventos)
            Directory.CreateDirectory(caminhoEventos);

         File.WriteAllText(configuracaoNF.CaminhoDownload.Insert(configuracaoNF.CaminhoDownload.Length, "\\Eventos") + "\\" + nota.Chave + "_ciencia-operacao.xml", xmlEvento);
         var servico = new CancelarServico(TAmb.Producao);
         var xmlRetorno = "";
         xmlRetorno = servico.Enviar(xmlEvento, estado, false, certificadoDigital, configuracaoNF.TempoLimiteSefaz);

         var xml1 = new XmlDocument();
         xml1.LoadXml(xmlRetorno);
         //if (xmlRetorno.Contains("Falha") && tipoEvento != TEventoTipo.CienciaDaOperacao)
         //   MessageBoxUtils.ExibeMensagemAdvertencia("Falha no evento. Falha na estrutura do XML. Consulte o suporte Técnico.");
         //if (xmlRetorno.ToLower().Contains("rejeicao") && tipoEvento != TEventoTipo.CienciaDaOperacao)
         //   throw new BusinessException(xml1.GetElementsByTagName("xMotivo")[1].InnerText.ToString());
         var existeNota = new ManifestacaoNFeBusiness().BuscarPorChave(nota.Chave);
         if (existeNota != null)
         {
            if (tipoEvento == TEventoTipo.ConfirmacaoDaOperacao)
               existeNota.idStatusManifestacao = (int)EnumStatusManifestacaoNFe.ConfirmacaoOperacao;
            else if (tipoEvento == TEventoTipo.CienciaDaOperacao)
               existeNota.idStatusManifestacao = (int)EnumStatusManifestacaoNFe.CienciaOperacao;
            else if (tipoEvento == TEventoTipo.DesconhecimentoDaOperacao)
               existeNota.idStatusManifestacao = (int)EnumStatusManifestacaoNFe.DesconhecimentoOperacao;
            else if (tipoEvento == TEventoTipo.OperacaoNaoRealizada)
               existeNota.idStatusManifestacao = (int)EnumStatusManifestacaoNFe.OperacaoNaoRealizada;

            new ManifestacaoNFeBusiness().Salvar(existeNota);
         }
         else
         {
            if (tipoEvento == TEventoTipo.ConfirmacaoDaOperacao)
               nota.idStatusManifestacao = (int)EnumStatusManifestacaoNFe.ConfirmacaoOperacao;
            else if (tipoEvento == TEventoTipo.CienciaDaOperacao)
               nota.idStatusManifestacao = (int)EnumStatusManifestacaoNFe.CienciaOperacao;
            else if (tipoEvento == TEventoTipo.DesconhecimentoDaOperacao)
               nota.idStatusManifestacao = (int)EnumStatusManifestacaoNFe.DesconhecimentoOperacao;
            else if (tipoEvento == TEventoTipo.OperacaoNaoRealizada)
               nota.idStatusManifestacao = (int)EnumStatusManifestacaoNFe.OperacaoNaoRealizada;

            new ManifestacaoNFeBusiness().Salvar(nota);
         }
         if (tipoEvento != TEventoTipo.CienciaDaOperacao)
            MessageBoxUtils.ExibeMensagem("Evento adicionado com sucesso!");
      }

      #endregion

      public override void Salvar(tb_nfe nf)
      {
         var repositorio = new NFeRepository();
         var nfeProdutoRepositorio = new NFeProdutoRepository();
         nf.tb_nf_status = null;

         var empresa = new EmpresaBusiness().BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);
         var cliente = new ClienteBusiness().BuscarPorId(nf.idCliente);

         foreach (var item in nf.tb_nfe_produto)
         {
            if (cliente.tb_cidade.tb_estado.Sigla == empresa.tb_cidade.tb_estado.Sigla)
            {
               item.idPartilhaICMS = null;
               item.PorcentagemIntraestadual = null;
               item.ValorIntraestadual = null;
               item.PorcentagemInterestadual = null;
               item.ValorInterestadual = null;
               item.PorcentagemFCP = null;
               item.ValorFCP = null;
            }
            item.tb_produto = null;
            item.tb_nfe = null;
            item.tb_cfop = null;
         }

         nf.InformacoesComplementares = StringUtis.RemoveCaracteresEspeciais(nf.InformacoesComplementares).Trim();
         if (nf.id == 0)
            repositorio.Salvar(nf);
         else
            repositorio.Atualizar(nf);

         AtualizarUltimoNumeroNota(nf.NumeroNota.ToInt32());
      }

      public void CriarNovaNota(tb_nfe nfe, object origem)
      {
         using (var transacao = new TransactionScope())
         {
            Salvar(nfe);
            if (origem is tb_compra)
            {
               tb_compra compra = origem as tb_compra;
               tb_compra_nfe compraNFe = new CompraNFeBusiness().BuscarPorIdCompra(compra.id);
               if (compraNFe == null) compraNFe = new tb_compra_nfe();
               compraNFe.idCompra = compra.id;
               compraNFe.idNFe = nfe.id;
               new CompraNFeBusiness().Salvar(compraNFe);

               /** Salva o numero da nota na tabela tb_movimento **/
               if (compra.idMovimento != null)
               {
                  tb_movimento movimento = new MovimentoBusiness().BuscarPorId(compra.idMovimento.ToInt32());
                  if (movimento != null)
                  {
                     movimento.NumeroNota = nfe.NumeroNota.ToString();
                     new MovimentoBusiness().Salvar(movimento);
                  }
               }
            }
            else if (origem is tb_venda || origem is List<tb_cupom_fiscal>)
            {
               tb_venda venda = null;
               if (origem is tb_venda)
                  venda = origem as tb_venda;
               else if (origem is List<tb_cupom_fiscal>)
                  venda = new VendaBusiness().BuscarPorId((origem as List<tb_cupom_fiscal>).FirstOrDefault().idVenda.GetValueOrDefault());
               if (venda != null)
               {
                  tb_venda_nfe vendaNFe = new VendaNFeBusiness().BuscarPorIdVenda(venda.id);
                  if (vendaNFe == null) vendaNFe = new tb_venda_nfe();
                  vendaNFe.idVenda = venda.id;
                  vendaNFe.idNFe = nfe.id;
                  new VendaNFeBusiness().Salvar(vendaNFe);

                  /** Salva o numero da nota na tabela tb_movimento **/
                  tb_venda_movimento vendaMovimento = new tb_venda_movimento();
                  vendaMovimento = new VendaMovimentoBusiness().BuscarPorIdVenda(venda.id);
                  if (vendaMovimento != null)
                  {
                     tb_movimento movimento = new MovimentoBusiness().BuscarPorId(vendaMovimento.idMovimento);
                     movimento.NumeroNota = nfe.NumeroNota.ToString();
                     new MovimentoBusiness().Salvar(movimento);
                  }
               }
            }
            else if (origem is tb_devolucao_compra)
            {
               tb_devolucao_compra devolucaoCompra = origem as tb_devolucao_compra;
               tb_devolucao_compra_nfe devolucaoCompraNFe = new DevolucaoCompraNFeBusiness().BuscarPorIdDevolucaoCompra(devolucaoCompra.id);
               if (devolucaoCompraNFe == null) devolucaoCompraNFe = new tb_devolucao_compra_nfe();
               devolucaoCompraNFe.idDevolucaoCompra = devolucaoCompra.id;
               devolucaoCompraNFe.idNFe = nfe.id;
               new DevolucaoCompraNFeBusiness().Salvar(devolucaoCompraNFe);

               /** Salva o numero da nota na tabela tb_movimento **/
               if (devolucaoCompra.idMovimento != null)
               {
                  tb_movimento movimento = new MovimentoBusiness().BuscarPorId(devolucaoCompra.idMovimento.ToInt32());
                  if (movimento != null)
                  {
                     movimento.NumeroNota = nfe.NumeroNota.ToString();
                     new MovimentoBusiness().Salvar(movimento);
                  }
               }
            }
            else if (origem is tb_devolucao_venda)
            {
               tb_devolucao_venda devolucaoVenda = origem as tb_devolucao_venda;
               tb_devolucao_venda_nfe devolucaoVendaNFe = new DevolucaoVendaNFeBusiness().BuscarPorIdDevolucaoVenda(devolucaoVenda.id);
               if (devolucaoVendaNFe == null) devolucaoVendaNFe = new tb_devolucao_venda_nfe();
               devolucaoVendaNFe.idDevolucaoVenda = devolucaoVenda.id;
               devolucaoVendaNFe.idNFe = nfe.id;
               new DevolucaoVendaNFeBusiness().Salvar(devolucaoVendaNFe);
            }
            else if (origem is tb_ordem_servico)
            {
               tb_ordem_servico ordemServico = origem as tb_ordem_servico;
               tb_ordem_servico_nfe ordemServicoNFe = new OrdemServicoNFeBusiness().BuscarPorIdOrdemServico(ordemServico.id);
               if (ordemServicoNFe == null) ordemServicoNFe = new tb_ordem_servico_nfe();
               ordemServicoNFe.idOrdemServico = ordemServico.id;
               ordemServicoNFe.idNFe = nfe.id;
               new OrdemServicoNFeBusiness().Salvar(ordemServicoNFe);

               /** Salva o numero da nota na tabela tb_movimento **/
               if (ordemServico.idMovimento != null)
               {
                  if (ordemServico.tb_movimento != null)
                  {
                     tb_movimento movimento = new MovimentoBusiness().BuscarPorId(ordemServico.idMovimento.ToInt32());
                     movimento.NumeroNota = nfe.NumeroNota.ToString();
                     new MovimentoBusiness().Salvar(movimento);
                  }
               }
            }

            transacao.Complete();
         }
      }
      public void AtualizarUltimoNumeroNota(int ultimoNumeroNota)
      {
         var parametro = new ParametroBusiness().BuscarParametroVigente();
         if (parametro.tb_nf_configuracao != null)
         {
            var configuracaoNF = parametro.tb_nf_configuracao;
            if (ultimoNumeroNota > configuracaoNF.UltimoNumeroNota)
            {
               configuracaoNF.UltimoNumeroNota = ultimoNumeroNota;
               new NFConfiguracaoBusiness().Atualizar(configuracaoNF);
            }
         }
         else
            throw new BusinessException("Favor salvar as configurações iniciais da NF-e no parâmetro");
      }

      public tb_nfe BuscarPorIdVenda(int idVenda)
      {
         var repositorio = new NFeRepository();
         return repositorio.BuscarPorIdVenda(idVenda);
      }
      public tb_nfe BuscarPorIdDevolucaoVenda(int idDevolucaoVenda)
      {
         var repositorio = new NFeRepository();
         return repositorio.BuscarPorIdDevolucaoVenda(idDevolucaoVenda);
      }
      public tb_nfe BuscarPorIdDevolucaoCompra(int idDevolucaoCompra)
      {
         var repositorio = new NFeRepository();
         return repositorio.BuscarPorIdDevolucaoCompra(idDevolucaoCompra);
      }
      public tb_nfe BuscarPorIdOrdemServico(int idServico)
      {
         var repositorio = new NFeRepository();
         return repositorio.BuscarPorIdOrdemServico(idServico);
      }
      public tb_nfe BuscarPorIdCompra(int idCompra)
      {
         var repositorio = new NFeRepository();
         return repositorio.BuscaPorIdCompra(idCompra);
      }
      public List<tb_nfe> ConsultarNotasVenda(DateTime dataInicio, DateTime dataFim, int? idStatus, int? idVenda, int? idCliente, int? idVendedor, int tipoNota, int tipoEmissao, int nroNFe)
      {
         return new NFeRepository().ConsultarNotasVenda(dataInicio, dataFim, idStatus, idVenda, idCliente, idVendedor, tipoNota, tipoEmissao, nroNFe);
      }

      public override tb_nfe BuscarPorId(int id)
      {
         return new NFeRepository().BuscarPorId(id);
      }
      public tb_nfe BuscarPorIdCliente(int id)
      {
         return new NFeRepository().BuscarPorIdCliente(id);
      }

      public override void Excluir(tb_nfe nf)
      {
         new NFeRepository().Excluir(nf);
      }

      public void CancelarPorIdVenda(int idVenda)
      {
         var nf = BuscarPorIdVenda(idVenda);
         if (nf != null)
         {
            nf.idNFStatus = (int)EnumStatusNFe.Cancelada;
            nf.tb_nf_status = null;
            Atualizar(nf);
         }
      }

      public tb_nfe BuscarPorChave(string chaveNFe)
      {
         return new NFeRepository().BuscarPorChave(chaveNFe);
      }

      public tb_nfe BuscarPorNumeroNota(int numeroNota, int numeroSerie)
      {
         return new NFeRepository().BuscarPorNumeroNota(numeroNota, numeroSerie);
      }
      public tb_nfe BuscarPoridNumeroNota(int numeroNota)
      {
         return new NFeRepository().BuscarPoridNumeroNota(numeroNota);
      }

      #region Consulta Notas Próprias

      public void ConsultarUltimasNotas()
      {
         Network net = new Network();
         if (net.IsAvailable == false)
         {
            throw new BusinessException("Sem Conexão Com a Internet!");
         }
         ConsultarNotasProprias();
      }

      public void ConsultarNotasProprias()
      {
         this.parametro = new ParametroBusiness().BuscarParametroVigente();
         var nsu = parametro.tb_nf_configuracao.UltimoNSU;
         if (string.IsNullOrEmpty(nsu) || nsu.ToInt32() == 0) nsu = NUMERO_INICIAL_NSU;
         var manifestacaoNFeBusiness = new ManifestacaoNFeBusiness();
         var eventoSoap = new PCInfo.Plus.Business.Classes.Soap.ConsultaNotasProprias.EventoSoap();
         var empresa = new EmpresaBusiness().BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);
         var estado = (TCodUfIBGE)Enum.Parse(typeof(TCodUfIBGE), empresa.tb_cidade.tb_estado.Sigla, true);
         var compraRepository = new CompraRepository();
         eventoSoap.header.nfeCabecMsg.cUF = estado;
         eventoSoap.header.nfeCabecMsg.versaoDados = "1.00";

         bool pararProcessamento = false;
         var nfeDistDFeInteresse = eventoSoap.body.nfeDistDFeInteresse;
         var nFeDadosMsg = nfeDistDFeInteresse.NFeDadosMsg;
         var distDFeInt = nFeDadosMsg.DistDFeInt;
         distDFeInt.UFAutor = (TCodUfIBGE)Enum.Parse(typeof(TCodUfIBGE), empresa.tb_cidade.tb_estado.Sigla, true);
         distDFeInt.Ambiente = TAmb.Producao;
         distDFeInt.CNPJ = empresa.NumeroInscricao;
         distDFeInt.DistNSU = new DistNSU();
         var distNSU = distDFeInt.DistNSU;
         distNSU.NSU = nsu;

         var xmlSoap = XmlUtils.Serializar<PCInfo.Plus.Business.Classes.Soap.ConsultaNotasProprias.EventoSoap>(eventoSoap);
         xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "nfeCabecMsg", "xmlns", "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe");
         xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "nfeDistDFeInteresse", "xmlns", "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe");
         xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "distDFeInt", "xmlns", "http://www.portalfiscal.inf.br/nfe");
         var certificadoDigital = new Certificado().Carregar(parametro.CertificadoDigital);
         PCInfo.Plus.Business.Classes.Soap.Manifestação.AssinaturaDigital assinatura = new PCInfo.Plus.Business.Classes.Soap.Manifestação.AssinaturaDigital();

         var servico = new ManifestacaoServico(TAmb.Producao);
         configuracaoNF = parametro.tb_nf_configuracao;
         var retorno = servico.Enviar(xmlSoap, TCodUfIBGE.NACIONAL, false, certificadoDigital, configuracaoNF.TempoLimiteSefaz);
         var docxml = new XmlDocument();
         if (retorno.Contains(@"Impossível conectar-se ao servidor remoto"))
            throw new BusinessException("Não foi possível conectar-se ao servidor. Verifique sua conexão de internet ou se o servidor encontra-se disponível!");
         docxml.LoadXml(retorno);
         var mensagemErro = docxml.GetElementsByTagName("xMotivo");
         if (mensagemErro.Count > 0 && (mensagemErro[0].InnerText.ToLower().Contains("erro")))
            throw new BusinessException(mensagemErro[0].InnerText);
         if (mensagemErro.Count > 0 && (mensagemErro[0].InnerText.ToLower().Contains("consumo")))
            throw new BusinessException("Quantidade de tentativas de consulta foi excedida.\nTente novamente mais tarde.");
         if (mensagemErro.Count > 0 && (mensagemErro[0].InnerText.ToLower().Contains("tempo limite")))
            throw new BusinessException("O tempo limite da operação foi atingido, tente novamente mais tarde.");

         var maxNSU = docxml.GetElementsByTagName("maxNSU")[0].InnerText;
         if (maxNSU != NUMERO_INICIAL_NSU && nsu == maxNSU)
            return;

         distNSU.NSU = maxNSU;
         int contador = 0;
         var quantidadeConsultar = maxNSU.ToInt32() - nsu.ToInt32();
         while (distNSU.NSU != NUMERO_INICIAL_NSU)
         {
            if (quantidadeConsultar > 50)
            {
               xmlSoap = XmlUtils.Serializar<PCInfo.Plus.Business.Classes.Soap.ConsultaNotasProprias.EventoSoap>(eventoSoap);
               xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "nfeCabecMsg", "xmlns", "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe");
               xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "nfeDistDFeInteresse", "xmlns", "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe");
               xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "distDFeInt", "xmlns", "http://www.portalfiscal.inf.br/nfe");
               certificadoDigital = new Certificado().Carregar(parametro.CertificadoDigital);
               assinatura = new PCInfo.Plus.Business.Classes.Soap.Manifestação.AssinaturaDigital();
               retorno = servico.Enviar(xmlSoap, TCodUfIBGE.NACIONAL, false, certificadoDigital, configuracaoNF.TempoLimiteSefaz);
               docxml = new XmlDocument();
               docxml.LoadXml(retorno);
            }
            XmlNodeList nodeList = docxml.GetElementsByTagName("docZip");

            if (retorno.Contains("Rejeicao") && !retorno.Contains("Consumo Indevido"))
               break;
            if (pararProcessamento) return;

            foreach (XmlNode node in nodeList)
            {
               byte[] file = System.Convert.FromBase64String(node.ChildNodes[0].Value);
               using (MemoryStream ms = new MemoryStream(file))
               {
                  using (GZipStream gzs = new GZipStream(ms, CompressionMode.Decompress))
                  {
                     using (StreamReader sr = new StreamReader(gzs))
                     {
                        var manifestacao = new tb_manifestacao_nfe();
                        var d = new XmlDocument();
                        d.LoadXml(sr.ReadToEnd());
                        if (d.InnerXml.ToString().StartsWith("<resNFe"))
                        {
                           var xmlRetorno = XmlUtils.Deserializar<tb_manifestacao_nfe>(d.InnerXml);
                           manifestacao.Chave = xmlRetorno.Chave;
                           manifestacao.CNPJ = xmlRetorno.CNPJ;
                           manifestacao.Emitente = xmlRetorno.Emitente;
                           manifestacao.DataEmissao = Convert.ToDateTime(xmlRetorno.DataEmissao);
                           var existeNota = manifestacaoNFeBusiness.BuscarPorChave(xmlRetorno.Chave);
                           if (existeNota != null)
                              manifestacao = existeNota;
                           else
                           {
                              manifestacao.idStatusManifestacao = (int)EnumStatusManifestacaoNFe.CienciaOperacao;
                              ManifestacaoOperacao(TEventoTipo.ConfirmacaoDaOperacao, manifestacao);
                           }
                           if (xmlRetorno.DataEmissao.Date < DateTime.Now.AddMonths(-1).Date && quantidadeConsultar > 50)
                           {
                              parametro.tb_nf_configuracao.UltimoNSU = maxNSU;
                              new NFConfiguracaoBusiness().Atualizar(parametro.tb_nf_configuracao);
                              pararProcessamento = true;
                           }
                        }
                     }
                  }
               }
               contador++;
            }
            if (quantidadeConsultar <= 0)
               return;
            if (quantidadeConsultar > 50)
               distNSU.NSU = (distNSU.NSU.ToInt32() - 50).ToString().PadLeft(15, '0');
            else
               distNSU.NSU = (distNSU.NSU.ToInt32() - quantidadeConsultar).ToString().PadLeft(15, '0');
            quantidadeConsultar -= 50;

            contador = 0;
            parametro.tb_nf_configuracao.UltimoNSU = maxNSU;
            new NFConfiguracaoBusiness().Atualizar(parametro.tb_nf_configuracao);
         }
      }
      #endregion

      #region Download Notas Proprias
      public ListasDownloadManifestacao DownloadNotasProprias(List<tb_manifestacao_nfe> listaNotasProprias)
      {
         ListasDownloadManifestacao listasDownloadManifestacao = new ListasDownloadManifestacao();
         if (listaNotasProprias != null)
            foreach (var item in listaNotasProprias)
            {
               this.parametro = new ParametroBusiness().BuscarParametroVigente();
               var eventoSoap = new PCInfo.Plus.Business.Classes.Soap.ConsultaNotasProprias.EventoSoap();
               var empresa = new EmpresaBusiness().BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);
               var estado = (TCodUfIBGE)Enum.Parse(typeof(TCodUfIBGE), empresa.tb_cidade.tb_estado.Sigla, true);
               eventoSoap.header.nfeCabecMsg.cUF = estado;
               eventoSoap.header.nfeCabecMsg.versaoDados = "1.01";

               var nfeDistDFeInteresse = eventoSoap.body.nfeDistDFeInteresse;
               var nFeDadosMsg = nfeDistDFeInteresse.NFeDadosMsg;
               var distDFeInt = nFeDadosMsg.DistDFeInt;
               distDFeInt.UFAutor = (TCodUfIBGE)Enum.Parse(typeof(TCodUfIBGE), empresa.tb_cidade.tb_estado.Sigla, true);
               distDFeInt.Ambiente = TAmb.Producao;
               distDFeInt.CNPJ = empresa.NumeroInscricao;
               distDFeInt.ConsChNFe = new ConsChNFe();
               distDFeInt.ConsChNFe.ChaveNFe = item.Chave;

               var xmlSoap = XmlUtils.Serializar<PCInfo.Plus.Business.Classes.Soap.ConsultaNotasProprias.EventoSoap>(eventoSoap);
               xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "nfeCabecMsg", "xmlns", "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe");
               xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "nfeDistDFeInteresse", "xmlns", "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe");
               xmlSoap = XmlUtils.SetarAtributoElemento(xmlSoap, "distDFeInt", "xmlns", "http://www.portalfiscal.inf.br/nfe");
               var certificadoDigital = new Certificado().Carregar(parametro.CertificadoDigital);

               PCInfo.Plus.Business.Classes.Soap.ConsultaNotasProprias.EventoSoap eventoSoapNSU = new PCInfo.Plus.Business.Classes.Soap.ConsultaNotasProprias.EventoSoap();
               var retorno = new ManifestacaoServico(TAmb.Producao).Enviar(xmlSoap, TCodUfIBGE.NACIONAL, false, certificadoDigital, parametro.tb_nf_configuracao.TempoLimiteSefaz);

               var docxml = new XmlDocument();
               docxml.LoadXml(retorno);
               if (docxml.InnerXml.ToLower().Contains("xmotivo"))
               {
                  var motivo = docxml.GetElementsByTagName("xMotivo");
                  if (motivo[0].InnerText.ToLower().Contains("documento localizado"))
                     listasDownloadManifestacao.listaNotasBaixadas.Add(item.Chave);
                  else
                  {
                     listasDownloadManifestacao.listaNotasNaoBaixadas.Add(item.Chave, motivo[0].InnerText);
                     continue;
                  }
               }

               XmlNodeList nodeList = docxml.GetElementsByTagName("docZip");

               foreach (XmlNode node in nodeList)
               {
                  byte[] file = System.Convert.FromBase64String(node.ChildNodes[0].Value);
                  using (MemoryStream ms = new MemoryStream(file))
                  {
                     using (GZipStream gzs = new GZipStream(ms, CompressionMode.Decompress))
                     {
                        using (StreamReader sr = new StreamReader(gzs))
                        {
                           var manifestacao = new tb_manifestacao_nfe();

                           configuracaoNF = parametro.tb_nf_configuracao;
                           if (parametro.tb_nf_configuracao == null)
                              throw new BusinessException("É necessário configurar um caminho para download do XML.");
                           else if (string.IsNullOrEmpty(configuracaoNF.CaminhoDownload))
                              throw new BusinessException("É necessário configurar um caminho para download do XML.");
                           var xmlRetorno = new XmlDocument();
                           xmlRetorno.LoadXml(sr.ReadToEnd());

                           var nfeProc = xmlRetorno.GetElementsByTagName("nfeProc");
                           if (nfeProc.Count > 0)
                           {
                              var caminhoDownload = configuracaoNF.CaminhoDownload.Insert(configuracaoNF.CaminhoDownload.Length, "\\Downloads");
                              var caminhoEventos = configuracaoNF.CaminhoDownload.Insert(configuracaoNF.CaminhoDownload.Length, "\\Eventos");
                              bool existePastaDownload = Directory.Exists(caminhoDownload);
                              if (!existePastaDownload)
                                 Directory.CreateDirectory(caminhoDownload);

                              bool existePastaEventos = Directory.Exists(caminhoEventos);
                              if (!existePastaEventos)
                                 Directory.CreateDirectory(caminhoEventos);

                              if (Directory.Exists(configuracaoNF.CaminhoDownload + "\\Downloads"))
                                 File.WriteAllText(configuracaoNF.CaminhoDownload.Insert(configuracaoNF.CaminhoDownload.Length, "\\Downloads") + "\\" + item.Chave + "_download.xml", xmlRetorno.InnerXml);
                              else
                                 throw new BusinessException("É necessário configurar um caminho para download do XML.");
                           }
                        }
                     }
                  }
               }
            }
         return listasDownloadManifestacao;
      }

      public class ListasDownloadManifestacao
      {
         public ListasDownloadManifestacao()
         {
            listaNotasNaoBaixadas = new Dictionary<string, string>();
            listaNotasBaixadas = new List<string>();
         }
         public Dictionary<string, string> listaNotasNaoBaixadas { get; set; }
         public List<string> listaNotasBaixadas { get; set; }
      }

      #endregion

      public List<tb_nfe> BuscarPorIntervaloNumeroNota(int numeroInicial, int numeroFinal)
      {
         return new NFeRepository().BuscarPorIntervaloNumeroNota(numeroInicial, numeroFinal);
      }

      public tb_nfe BuscarUltimaNota()
      {
         return new NFeRepository().BuscarUltimaNota();
      }

      public List<tb_nfe> BuscarUltimaNotaEmitidaComSucessoPorNumeroNota(int NumeroNota)
      {
         return new NFeRepository().BuscarUltimaNotaEmitidaComSucessoPorNumeroNota(NumeroNota);
      }

      public tb_nfe BuscarUltimaNotaEmitidaComSucessoPorDataEmissaoNumeroNota(DateTime sDataEmissao, int NumeroNota, int idEmpresa)
      {
         return new NFeRepository().BuscarUltimaNotaEmitidaComSucessoPorDataEmissaoNumeroNota(sDataEmissao, NumeroNota, idEmpresa);
      }

      public List<tb_nfe> BuscarNotasSINTEGRA(DateTime dataInicial, DateTime dataFinal)
      {
         return new NFeRepository().BuscarPorPeriodo(dataInicial, dataFinal);
      }
      public List<tb_nfe> BuscarNotasSped(DateTime dataInicial, DateTime dataFinal)
      {
         return new NFeRepository().BuscarPorPeriodoSped(dataInicial, dataFinal);
      }

      public static string RemoveCaracteresEspeciaisNFe(string texto)
      {
         var resultado = Regex.Replace(texto, @"[^A-Za-z0-9 _\$\.\,()-\/:%]", " ").Replace("  ", " ");
         return resultado.ToString();
      }
      public static string RemoverAcentos(string texto)
      {
         if (string.IsNullOrEmpty(texto))
            return String.Empty;

         byte[] bytes = System.Text.Encoding.GetEncoding("iso-8859-8").GetBytes(texto);
         return System.Text.Encoding.UTF8.GetString(bytes);
      }

      public bool ValidarChaveNFe(string chaveNFe)
      {
         if (!string.IsNullOrEmpty(chaveNFe))
         {
            string digitoVerificadorDigitado = string.Empty;
            if (chaveNFe.Length != 44)
               return false;
            else
            {
               digitoVerificadorDigitado = chaveNFe.Substring(43, 1);
               chaveNFe = chaveNFe.Substring(0, 43);
            }

            return true;
         }
         else
            return true;
      }

      public void ValidarValidadeCertificado(X509Certificate certificado)
      {
         if (certificado != null)
         {
            string dataValidadeString = certificado.GetExpirationDateString();
            if (!string.IsNullOrEmpty(dataValidadeString))
            {
               DateTime dataValidade = dataValidadeString.ToDateTime();
               if (DateTime.Now > dataValidade)
                  throw new BusinessException("Certificado digital inválido!\nFavor consultar a empresa emissora do certificado solicitando um novo certificado válido.\nCertificado vencido dia: " + dataValidadeString);
            }
         }
      }

   }
}