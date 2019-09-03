using DFe.Classes.Flags;
using PCInfo.Base;
using PCInfo.Base.Business;
using PCInfo.Base.ClassesUtilitarias;
using PCInfo.Controles;
using PCInfo.Plus.Business;
using PCInfo.Plus.Business.Classes.Informacoes.Destinatario;
using PCInfo.Plus.Business.Classes.Informacoes.Detalhe;
using PCInfo.Plus.Business.Classes.Informacoes.Identificacao.Tipos;
using PCInfo.Plus.Business.Classes.Informacoes.Transporte;
using PCInfo.Plus.Business.Classes.Soap.Enumeradores;
using PCInfo.Plus.Main.Classes;
using PCInfo.Plus.Main.UI.Popups;
using PCInfo.Plus.Main.UI.Relatorios;
using PCInfo.Plus.Menu;
using PCInfo.Plus.Model;
using PCInfo.Plus.Model.Models;
using PCInfo.Plus.Model.Models.Enumeradores;
using PCInfo.Plus.Models.Enumeradores;
using PCInfo.Plus.Utils.Enumeradores;
using PCInfo.Utilitarios;
using PCInfo.Utils;
using PCInfo.Utils.Enumeradores;
using PCInfo.Utils.Exceptions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCInfo.Plus.Main.UI.Movimentos.NotaFiscal
{
   /// <summary>
   /// Interação lógica para usrEmissaoNFe.xam
   /// </summary>
   public partial class usrEmissaoNFe : usrBase
   {
      private tb_venda venda;
      private tb_compra compra;
      private tb_devolucao_compra devolucaoCompra;
      private tb_devolucao_venda devolucaoVenda;
      private tb_ordem_servico ordemServico;
      tb_nfe nfe;
      NFeBusiness nfeBusiness;
      tb_parametro parametro;
      EmpresaBusiness empresaBusiness;
      tb_empresa empresa;
      List<tb_cfop> listaCFOPS;
      tb_estado estadoCliente;
      tb_cliente cliente;
      int TipoNota;
      tb_produto produto;
      tb_nfe_produto produtoSelecionado;
      ObservableCollection<tb_nfe_produto> nfeProdutoObservable;
      private ObservableCollection<tb_nfe_formapagamento> formaPagamentoObservable;
      private ObservableCollection<tb_nfe_pagamento> pagamentoObservable;
      ClienteBusiness clienteBusiness;
      private ObservableCollection<tb_nfe_referencia> nfeReferenciaObservable;
      private ObservableCollection<tb_nfe_referencia_cupom> nfeReferenciaCupomObservable;
      private ObservableCollection<tb_nfe_declaracao_importacao_adicao> adicaoObservable;
      private ObservableCollection<tb_nfe_referencia_contranota> nfeReferenciaContranotaObservable;
      private int? idTipoPag = null;
      ProdutoBusiness produtoBusiness;
      tb_partilha_icms partilhaICMS;
      private decimal? aliquotaIntraestadual;
      private decimal? aliquotaInterestadual;
      private int NumeroLoteCont;
      public usrEmissaoNFe()
      {
         InitializeComponent();
         txtChave.IsReadOnly = true;
         ConfiguracoesIniciais();
      }

      #region Receber parametro de outras telas como: Venda,Ordem de Serviço,Compra,Devoluções e Consulta NFe
      public override void ReceberParametro(object item)
      {
         //recebe dados da tela consulta cupom e insere na aba de referencia devolução.
         if (item is tb_nfe_referencia_cupom)
         {
            var refCupom = item as tb_nfe_referencia_cupom;
            if (refCupom.idNFe > 0)
               nfe = new NFeBusiness().BuscarPorId(refCupom.idNFe);

            if (nfe != null && nfe.id > 0)
            {
               txtNumeroNota.Text = nfe.NumeroNota.ToString();
               if (nfe.Ambiente == 2)
                  cboAmbiente.SelectedValue = TipoAmbiente.Homologacao;
               else
                  cboAmbiente.SelectedValue = TipoAmbiente.Producao;
               txtStatus.Text = nfe.DescricaoStatus;
               txtStatus.Foreground = AlterarCorStatus(nfe.DescricaoStatus);
            }
            txtOrigemNfe.Text = "Devolução de Cupom Fiscal";
            cboFinalidade.SelectedValue = (int)FinalidadeNFe.fnDevolucao;
            cboTipoNota.SelectedValue = (int)EnumTipoNota.DevolucaoVenda;
            rdbDevolucaoCupom.IsChecked = true;
            tbiReferenciaDevolucao.Visibility = Visibility.Visible;
            cboModeloEcf.SelectedValue = "2D";
            txtNumeroSequencial.Focus();
            tb_nfe_referencia_cupom cupom = new tb_nfe_referencia_cupom();
            cupom.Modelo = "2D";
            cupom.NumeroECF = refCupom.NumeroECF;
            cupom.NumeroCupom = refCupom.NumeroCupom;
            ValidarCamposObrigatorioReferenciaCupom(cupom);
            nfeReferenciaCupomObservable.Add(cupom);
            dgReferenciaCupom.ItemsSource = nfeReferenciaCupomObservable;
            TipoNota = (int)EnumTipoNota.DevolucaoVenda;

            if (refCupom.idNFe > 0)
            {
               PreencherDadosProdutoOrigemTelaConsulta(refCupom);
               PreencherCamposFrete(refCupom);
               PreencherDadosPagamentoOrigemTelaConsulta(refCupom);
               PreencherInformacaoComplementar(refCupom);
            }
            else
               PreencherDadosProdutoCupom(refCupom);
         }
         //dados vem da tela consulta de fornecedor
         if(item is tb_cliente)
         {
            cliente = item as tb_cliente;
            cboFinalidade.SelectedValue = (int)FinalidadeNFe.fnDevolucao;
            cboTipoNota.SelectedValue = (int)EnumTipoNota.DevolucaoCompra;
            uscClienteDestinatario.Id = cliente.id; 
         }
         //dados vem da tela de venda
         if (item is tb_venda)
         {
            venda = item as tb_venda;

            nfe = new NFeBusiness().BuscarPorIdVenda(venda.id);
            if (nfe != null)
            {
               txtChave.Text = nfe.Chave;
               txtNumeroNota.Text = nfe.NumeroNota.ToString();
               if (nfe.Ambiente == 2)
                  cboAmbiente.SelectedValue = TipoAmbiente.Homologacao;
               else
                  cboAmbiente.SelectedValue = TipoAmbiente.Producao;

               txtOrigemNfe.Text = nfe.DescricaoTipoNota;
               txtStatus.Text = nfe.DescricaoStatus;
               txtStatus.Foreground = AlterarCorStatus(nfe.DescricaoStatus);
               if (nfe.idNFStatus.In((int)EnumStatusNFe.Cancelada, (int)EnumStatusNFe.CanceladaSemEmissao, (int)EnumStatusNFe.Denegada, (int)EnumStatusNFe.EmitidaComSucesso, (int)EnumStatusNFe.Inutilizada))
               {
                  btnTransmitir.IsEnabled = false;
                  btnSalvar.IsEnabled = false;
                  btnNovaNfe.IsEnabled = true;
               }

               if (nfe.idNFStatus.In((int)EnumStatusNFe.EmitidaComSucesso))
               {
                  tbiTotais.IsEnabled = true;
                  btnVisualizarDanfe.IsEnabled = true;
                  CalcularTotais(nfe);
               }
               PreencherDadosProdutoOrigemTelaConsulta(null, null, null, nfe);
               PreencherDadosPagamentoOrigemTelaConsulta(null, null, null, nfe);
               PreencherCamposFrete(null, null, null, nfe);
               PreencherInformacaoComplementar(null, null, null, nfe);
               PreencherErros();

               cboTipoNota.SelectedValue = (int)nfe.TipoNota;
               TipoNota = (int)nfe.TipoNota;
               uscClienteDestinatario.Id = nfe.idCliente;
               cboFinalidade.SelectedValue = (FinalidadeNFe)nfe.CodigoFinalidade;
               cboTipoEmissao.SelectedValue = (TipoEmissao)nfe.TipoEmissao;
               txtDataEmissao.Text = nfe.DataEmissao.ToShortDateString();
            }
            else
            {
               nfe = new tb_nfe();
               txtOrigemNfe.Text = "Venda";
               cboFinalidade.SelectedValue = (int)FinalidadeNFe.fnNormal;
               cboTipoNota.SelectedValue = (int)EnumTipoNota.Saida;
               TipoNota = (int)EnumTipoNota.Saida;
               uscClienteDestinatario.Id = venda.idCliente;
               PreencherDadosProduto(venda.tb_venda_produto);
               PreencherDadosPagamento(venda);
            }
         }
         //dados vem da ordem de serviço
         if (item is tb_ordem_servico)
         {
            ordemServico = item as tb_ordem_servico;
            nfe = new NFeBusiness().BuscarPorIdOrdemServico(ordemServico.id);
            if (nfe != null)
            {
               txtChave.Text = nfe.Chave;
               txtNumeroNota.Text = nfe.NumeroNota.ToString();
               if (nfe.Ambiente == 2)
                  cboAmbiente.SelectedValue = TipoAmbiente.Homologacao;
               else
                  cboAmbiente.SelectedValue = TipoAmbiente.Producao;

               txtOrigemNfe.Text = nfe.DescricaoTipoNota;
               txtStatus.Text = nfe.DescricaoStatus;
               txtStatus.Foreground = AlterarCorStatus(nfe.DescricaoStatus);
               if (nfe.idNFStatus.In((int)EnumStatusNFe.Cancelada, (int)EnumStatusNFe.CanceladaSemEmissao, (int)EnumStatusNFe.Denegada, (int)EnumStatusNFe.EmitidaComSucesso, (int)EnumStatusNFe.Inutilizada))
               {
                  btnTransmitir.IsEnabled = false;
                  btnSalvar.IsEnabled = false;
                  btnNovaNfe.IsEnabled = true;
               }

               if (nfe.idNFStatus.In((int)EnumStatusNFe.EmitidaComSucesso))
               {
                  tbiTotais.IsEnabled = true;
                  btnVisualizarDanfe.IsEnabled = true;
                  CalcularTotais(nfe);
               }
               PreencherDadosProdutoOrigemTelaConsulta(null, null, null, nfe);
               PreencherDadosPagamentoOrigemTelaConsulta(null, null, null, nfe);
               PreencherCamposFrete(null, null, null, nfe);
               PreencherInformacaoComplementar(null, null, null, nfe);
               PreencherErros();

               cboTipoNota.SelectedValue = (int)nfe.TipoNota;
               TipoNota = (int)nfe.TipoNota;
               uscClienteDestinatario.Id = nfe.idCliente;
               cboFinalidade.SelectedValue = (FinalidadeNFe)nfe.CodigoFinalidade;
               cboTipoEmissao.SelectedValue = (TipoEmissao)nfe.TipoEmissao;
               txtDataEmissao.Text = nfe.DataEmissao.ToShortDateString();
            }
            else
            {
               nfe = new tb_nfe();
               txtOrigemNfe.Text = "Ordem de Serviço";
               cboFinalidade.SelectedValue = (int)FinalidadeNFe.fnNormal;
               cboTipoNota.SelectedValue = (int)EnumTipoNota.Saida;
               TipoNota = (int)EnumTipoNota.Saida;
               uscClienteDestinatario.Id = ordemServico.idCliente;
               PreencherDadosProdutoOrdemServico(ordemServico.tb_ordem_servico_produto);
               PreencherDadosPagamento(null, ordemServico);
            }
         }

         //dados vem da tela de compra>> entrada própria
         if (item is tb_compra)
         {
            compra = item as tb_compra;
            nfe = new NFeBusiness().BuscarPorIdCompra(compra.id);
            if (nfe != null)
            {
               txtChave.Text = nfe.Chave;
               txtNumeroNota.Text = nfe.NumeroNota.ToString();
               if (nfe.Ambiente == 2)
                  cboAmbiente.SelectedValue = TipoAmbiente.Homologacao;
               else
                  cboAmbiente.SelectedValue = TipoAmbiente.Producao;

               txtOrigemNfe.Text = nfe.DescricaoTipoNota;
               txtStatus.Text = nfe.DescricaoStatus;
               txtStatus.Foreground = AlterarCorStatus(nfe.DescricaoStatus);
               if (nfe.idNFStatus.In((int)EnumStatusNFe.Cancelada, (int)EnumStatusNFe.CanceladaSemEmissao, (int)EnumStatusNFe.Denegada, (int)EnumStatusNFe.EmitidaComSucesso, (int)EnumStatusNFe.Inutilizada))
               {
                  btnTransmitir.IsEnabled = false;
                  btnSalvar.IsEnabled = false;
                  btnNovaNfe.IsEnabled = true;
               }

               if (nfe.idNFStatus.In((int)EnumStatusNFe.EmitidaComSucesso))
               {
                  tbiTotais.IsEnabled = true;
                  btnVisualizarDanfe.IsEnabled = true;
                  CalcularTotais(nfe);
               }
               PreencherDadosProdutoOrigemTelaConsulta(null, null, null, nfe);
               PreencherDadosPagamentoOrigemTelaConsulta(null, null, null, nfe);
               PreencherCamposFrete(null, null, null, nfe);
               PreencherInformacaoComplementar(null, null, null, nfe);
               PreencherErros();

               cboTipoNota.SelectedValue = (int)nfe.TipoNota;
               TipoNota = (int)nfe.TipoNota;
               uscClienteDestinatario.Id = nfe.idCliente;
               cboFinalidade.SelectedValue = (FinalidadeNFe)nfe.CodigoFinalidade;
               cboTipoEmissao.SelectedValue = (TipoEmissao)nfe.TipoEmissao;
               txtDataEmissao.Text = nfe.DataEmissao.ToShortDateString();
               //inserir dados contranota
               var referenciaContranota = new NFeReferenciaContranotaBusiness().BuscarPorIdNota(nfe.id);
               if (cboTipoNota.SelectedIndex == (int)EnumTipoNota.Entrada && referenciaContranota.Count > 0)
               {
                  foreach (tb_nfe_referencia_contranota itemContranota in referenciaContranota)
                  {
                     nfeReferenciaContranotaObservable.Add(itemContranota);
                  }
                  dgContranotaReferencia.ItemsSource = nfeReferenciaContranotaObservable;

               }
               HabilitarReferencia();
            }
            else
            {
               nfe = new tb_nfe();
               txtOrigemNfe.Text = "Compra";
               cboTipoNota.SelectedValue = (int)EnumTipoNota.Entrada;
               TipoNota = (int)EnumTipoNota.Entrada;
               var clienteE = clienteBusiness.BuscarPorInscricao((EnumTipoInscricao)empresa.TipoInscricao, empresa.NumeroInscricao);

               if (parametro.EntradaPropriaPropriaEmpresa == false || clienteE == null)//escolhido cliente no parametro
               {
                  var fornecedor = new FornecedorBusiness().BuscarPorId(compra.idFornecedor);
                  var cliente = clienteBusiness.BuscarPorInscricao((EnumTipoInscricao)fornecedor.TipoInscricao, fornecedor.NumeroInscricao);
                  if (cliente == null)
                  {
                     clienteBusiness.CriarClientePorFornecedor(fornecedor);
                     cliente = clienteBusiness.BuscarPorInscricao((EnumTipoInscricao)fornecedor.TipoInscricao, fornecedor.NumeroInscricao);
                  }
                  if (cliente != null)
                  {
                     estadoCliente = new EstadoBusiness().BuscarPorId(cliente.tb_cidade.idEstado);
                     uscClienteDestinatario.Id = cliente.id;
                  }
               }
               else
               {
                  if (clienteE != null)
                  {
                     estadoCliente = new EstadoBusiness().BuscarPorId(clienteE.tb_cidade.idEstado);
                     uscClienteDestinatario.Id = clienteE.id;
                  }
               }
               cboFinalidade.SelectedValue = FinalidadeNFe.fnNormal;
               PreencherDadosProdutoCompra(compra.tb_compra_produto);
               PreencherDadosPagamento(null, null, compra);
               HabilitarReferencia();
            }
         }
         //dados vem da tela de devolução de compra
         if (item is tb_devolucao_compra)
         {
            devolucaoCompra = item as tb_devolucao_compra;
            nfe = new NFeBusiness().BuscarPorIdDevolucaoCompra(devolucaoCompra.id);
            if (nfe != null)
            {
               txtNumeroNota.Text = nfe.NumeroNota.ToString();
               if (nfe.Ambiente == 2)
                  cboAmbiente.SelectedValue = TipoAmbiente.Homologacao;
               else
                  cboAmbiente.SelectedValue = TipoAmbiente.Producao;

               txtChave.Text = nfe.Chave;
               txtOrigemNfe.Text = "Devolução Compra";
               txtStatus.Text = nfe.DescricaoStatus;
               txtStatus.Foreground = AlterarCorStatus(nfe.DescricaoStatus);
               if (nfe.idNFStatus.In((int)EnumStatusNFe.Cancelada, (int)EnumStatusNFe.CanceladaSemEmissao, (int)EnumStatusNFe.Denegada, (int)EnumStatusNFe.EmitidaComSucesso, (int)EnumStatusNFe.Inutilizada))
               {
                  btnTransmitir.IsEnabled = false;
                  btnSalvar.IsEnabled = false;
                  btnNovaNfe.IsEnabled = true;
               }
               if (nfe.idNFStatus.In((int)EnumStatusNFe.EmitidaComSucesso))
               {
                  tbiTotais.IsEnabled = true;
                  btnVisualizarDanfe.IsEnabled = true;
                  CalcularTotais(nfe);
               }
               cboTipoNota.SelectedValue = (int)EnumTipoNota.DevolucaoCompra;
               TipoNota = (int)EnumTipoNota.DevolucaoCompra;
               uscClienteDestinatario.Id = devolucaoCompra.idCliente;
               cboFinalidade.SelectedValue = (int)FinalidadeNFe.fnDevolucao;
               rdbDevolucaoNota.IsChecked = true;
               tbiReferenciaDevolucao.Visibility = Visibility.Visible;

               foreach (var itemDevolucaoCompraProduto in devolucaoCompra.tb_devolucao_compra_produto)
               {
                  tb_nfe_referencia referenciaChaveCompra = new tb_nfe_referencia();
                  referenciaChaveCompra.Chave = itemDevolucaoCompraProduto.ChaveReferencia;

                  nfeReferenciaObservable.Add(referenciaChaveCompra);
               }

               dgChaveReferencia.ItemsSource = nfeReferenciaObservable;

               PreencherDadosProdutoOrigemTelaConsulta(null, null, null, nfe);
               PreencherDadosPagamentoOrigemTelaConsulta(null, null, null, nfe);
               PreencherCamposFrete(null, null, null, nfe);
               PreencherInformacaoComplementar(null, null, null, nfe);
            }
            else
            {
               nfe = new tb_nfe();
               txtOrigemNfe.Text = "Devolução Compra";
               cboTipoNota.SelectedValue = (int)EnumTipoNota.DevolucaoCompra;
               TipoNota = (int)EnumTipoNota.DevolucaoCompra;
               uscClienteDestinatario.Id = devolucaoCompra.idCliente;
               cboFinalidade.SelectedValue = FinalidadeNFe.fnDevolucao;
               rdbDevolucaoNota.IsChecked = true;
               tbiReferenciaDevolucao.Visibility = Visibility.Visible;
               foreach (var itemDevolucaoCompraProduto in devolucaoCompra.tb_devolucao_compra_produto)
               {
                  tb_nfe_referencia referenciaChaveCompra = new tb_nfe_referencia();
                  referenciaChaveCompra.Chave = itemDevolucaoCompraProduto.ChaveReferencia;

                  nfeReferenciaObservable.Add(referenciaChaveCompra);
               }
               dgChaveReferencia.ItemsSource = nfeReferenciaObservable;

               PreencherDadosProdutoDevolucaoCompra(devolucaoCompra.tb_devolucao_compra_produto);
               PreencherDadosPagamento(null, null, null, devolucaoCompra);
            }
         }
         //dados vem da tela de devolução de venda
         if (item is tb_devolucao_venda)
         {
            devolucaoVenda = item as tb_devolucao_venda;
            nfe = new NFeBusiness().BuscarPorIdDevolucaoVenda(devolucaoVenda.id);
            if (nfe != null)
            {
               txtChave.Text = nfe.Chave;
               txtNumeroNota.Text = nfe.NumeroNota.ToString();
               if (nfe.Ambiente == 2)
                  cboAmbiente.SelectedValue = TipoAmbiente.Homologacao;
               else
                  cboAmbiente.SelectedValue = TipoAmbiente.Producao;

               txtOrigemNfe.Text = nfe.DescricaoTipoNota;
               txtStatus.Text = nfe.DescricaoStatus;
               txtStatus.Foreground = AlterarCorStatus(nfe.DescricaoStatus);
               if (nfe.idNFStatus.In((int)EnumStatusNFe.Cancelada, (int)EnumStatusNFe.CanceladaSemEmissao, (int)EnumStatusNFe.Denegada, (int)EnumStatusNFe.EmitidaComSucesso, (int)EnumStatusNFe.Inutilizada))
               {
                  btnTransmitir.IsEnabled = false;
                  btnSalvar.IsEnabled = false;
                  btnNovaNfe.IsEnabled = true;
               }

               if (nfe.idNFStatus.In((int)EnumStatusNFe.EmitidaComSucesso))
               {
                  tbiTotais.IsEnabled = true;
                  btnVisualizarDanfe.IsEnabled = true;
                  CalcularTotais(nfe);
               }

               foreach (var itemDevolucaoVendaProduto in devolucaoVenda.tb_devolucao_venda_produto)
               {
                  tb_nfe_referencia referenciaChave = new tb_nfe_referencia();
                  referenciaChave.Chave = itemDevolucaoVendaProduto.ChaveReferencia;

                  nfeReferenciaObservable.Add(referenciaChave);
               }

               dgChaveReferencia.ItemsSource = nfeReferenciaObservable;

               PreencherDadosProdutoOrigemTelaConsulta(null, null, null, nfe);
               PreencherDadosPagamentoOrigemTelaConsulta(null, null, null, nfe);
               PreencherCamposFrete(null, null, null, nfe);
               PreencherInformacaoComplementar(null, null, null, nfe);
               PreencherErros();

               cboTipoNota.SelectedValue = (int)nfe.TipoNota;
               TipoNota = (int)nfe.TipoNota;
               uscClienteDestinatario.Id = nfe.idCliente;
               cboFinalidade.SelectedValue = (FinalidadeNFe)nfe.CodigoFinalidade;
               cboTipoEmissao.SelectedValue = (TipoEmissao)nfe.TipoEmissao;
               txtDataEmissao.Text = nfe.DataEmissao.ToShortDateString();
            }
            else
            {
               nfe = new tb_nfe();
               devolucaoVenda = item as tb_devolucao_venda;
               txtOrigemNfe.Text = "Devolução Venda";
               cboTipoNota.SelectedValue = (int)EnumTipoNota.DevolucaoVenda;
               TipoNota = (int)EnumTipoNota.DevolucaoVenda;
               uscClienteDestinatario.Id = devolucaoVenda.idCliente;
               cboFinalidade.SelectedValue = FinalidadeNFe.fnDevolucao;
               rdbDevolucaoNota.IsChecked = true;
               tbiReferenciaDevolucao.Visibility = Visibility.Visible;

               foreach (var itemDevolucaoVendaProduto in devolucaoVenda.tb_devolucao_venda_produto)
               {
                  tb_nfe_referencia referenciaChave = new tb_nfe_referencia();
                  referenciaChave.Chave = itemDevolucaoVendaProduto.ChaveReferencia;

                  nfeReferenciaObservable.Add(referenciaChave);
               }

               dgChaveReferencia.ItemsSource = nfeReferenciaObservable;

               PreencherDadosProdutoDevolucaoVenda(devolucaoVenda.tb_devolucao_venda_produto);
               PreencherDadosPagamento(null, null, null, null, devolucaoVenda);
            }
         }
         //dados vem da tela de devolução
         if (item is tb_devolucao_venda_nfe)
         {
            var DevolucaoVenda = item as tb_devolucao_venda_nfe;

            nfe = new NFeBusiness().BuscarPorId(DevolucaoVenda.idNFe);
            if (nfe.id > 0)
            {
               txtNumeroNota.Text = nfe.NumeroNota.ToString();
               if (nfe.Ambiente == 2)
                  cboAmbiente.SelectedValue = TipoAmbiente.Homologacao;
               else
                  cboAmbiente.SelectedValue = TipoAmbiente.Producao;
            }
            txtOrigemNfe.Text = "Devolução Venda";
            txtStatus.Text = nfe.DescricaoStatus;
            txtStatus.Foreground = AlterarCorStatus(nfe.DescricaoStatus);
            if (nfe.idNFStatus.In((int)EnumStatusNFe.Cancelada, (int)EnumStatusNFe.CanceladaSemEmissao, (int)EnumStatusNFe.Denegada, (int)EnumStatusNFe.EmitidaComSucesso, (int)EnumStatusNFe.Inutilizada))
            {
               btnTransmitir.IsEnabled = false;
               btnSalvar.IsEnabled = false;
               btnNovaNfe.IsEnabled = true;
            }
            cboTipoNota.SelectedValue = (int)EnumTipoNota.DevolucaoVenda;
            TipoNota = (int)EnumTipoNota.DevolucaoVenda;
            cboFinalidade.SelectedValue = (int)FinalidadeNFe.fnDevolucao;
            uscClienteDestinatario.Id = DevolucaoVenda.tb_devolucao_venda.idCliente;
            rdbDevolucaoNota.IsChecked = true;
            tbiReferenciaDevolucao.Visibility = Visibility.Visible;
            tb_nfe_referencia referenciaChave = new tb_nfe_referencia();
            foreach (var dev in DevolucaoVenda.tb_devolucao_venda.tb_devolucao_venda_produto)
            {
               referenciaChave = new tb_nfe_referencia();
               referenciaChave.Chave = dev.ChaveReferencia;
               nfeReferenciaObservable.Add(referenciaChave);
            } 
            dgChaveReferencia.ItemsSource = nfeReferenciaObservable;
            PreencherDadosProdutoOrigemTelaConsulta(null, DevolucaoVenda);
            PreencherDadosPagamentoOrigemTelaConsulta(null, DevolucaoVenda);
            PreencherCamposFrete(null, DevolucaoVenda);
            PreencherInformacaoComplementar(null, DevolucaoVenda);
         }
         if (item is tb_devolucao_compra_nfe)
         {
            var DevolucaoCompra = item as tb_devolucao_compra_nfe;

            nfe = new NFeBusiness().BuscarPorId(DevolucaoCompra.idNFe);
            if (nfe.id > 0)
            {
               txtNumeroNota.Text = nfe.NumeroNota.ToString();
               if (nfe.Ambiente == 2)
                  cboAmbiente.SelectedValue = TipoAmbiente.Homologacao;
               else
                  cboAmbiente.SelectedValue = TipoAmbiente.Producao;
            }
            txtChave.Text = nfe.Chave;
            txtOrigemNfe.Text = "Devolução Compra";
            txtStatus.Text = nfe.DescricaoStatus;
            txtStatus.Foreground = AlterarCorStatus(nfe.DescricaoStatus);
            if (nfe.idNFStatus.In((int)EnumStatusNFe.Cancelada, (int)EnumStatusNFe.CanceladaSemEmissao, (int)EnumStatusNFe.Denegada, (int)EnumStatusNFe.EmitidaComSucesso, (int)EnumStatusNFe.Inutilizada))
            {
               btnTransmitir.IsEnabled = false;
               btnSalvar.IsEnabled = false;
               btnNovaNfe.IsEnabled = true;
            }
            cboTipoNota.SelectedValue = (int)EnumTipoNota.DevolucaoCompra;
            TipoNota = (int)EnumTipoNota.DevolucaoCompra;
            cboFinalidade.SelectedValue = (int)FinalidadeNFe.fnDevolucao;
            uscClienteDestinatario.Id = DevolucaoCompra.tb_devolucao_compra.idCliente;
            rdbDevolucaoNota.IsChecked = true;
            tbiReferenciaDevolucao.Visibility = Visibility.Visible;
            tb_nfe_referencia referenciaChave = new tb_nfe_referencia();
            foreach (var dev in DevolucaoCompra.tb_devolucao_compra.tb_devolucao_compra_produto)
            {
               referenciaChave = new tb_nfe_referencia();
               referenciaChave.Chave =dev.ChaveReferencia;
               nfeReferenciaObservable.Add(referenciaChave);
            }            
            dgChaveReferencia.ItemsSource = nfeReferenciaObservable;
            PreencherDadosProdutoOrigemTelaConsulta(null, null, DevolucaoCompra);
            PreencherDadosPagamentoOrigemTelaConsulta(null, null, DevolucaoCompra);
            PreencherCamposFrete(null, null, DevolucaoCompra);
            PreencherInformacaoComplementar(null, null, DevolucaoCompra);
         }
         //Dados vem direto da tela de consulta nfe
         if (item is tb_nfe)
         {
            var NFe = item as tb_nfe;

            nfe = NFe;
            if (nfe.id > 0)
            {
               txtChave.Text = nfe.Chave;
               txtNumeroNota.Text = nfe.NumeroNota.ToString();
               txtDataSaida.Text = nfe.DataEntradaSaida.ToShortDateString();
               if (nfe.Ambiente == 2)
                  cboAmbiente.SelectedValue = TipoAmbiente.Homologacao;
               else
                  cboAmbiente.SelectedValue = TipoAmbiente.Producao;
            }
            txtOrigemNfe.Text = nfe.DescricaoTipoNota;
            txtStatus.Text = nfe.DescricaoStatus;
            txtStatus.Foreground = AlterarCorStatus(nfe.DescricaoStatus);
            if (nfe.idNFStatus.In((int)EnumStatusNFe.Cancelada, (int)EnumStatusNFe.CanceladaSemEmissao, (int)EnumStatusNFe.Denegada, (int)EnumStatusNFe.EmitidaComSucesso, (int)EnumStatusNFe.Inutilizada))
            {
               btnTransmitir.IsEnabled = false;
               btnSalvar.IsEnabled = false;
               btnNovaNfe.IsEnabled = true;
            }
            cboTipoNota.SelectedValue = (int)nfe.TipoNota;
            TipoNota = (int)nfe.TipoNota;
            uscClienteDestinatario.Id = nfe.idCliente;
            cboFinalidade.SelectedValue = (FinalidadeNFe)nfe.CodigoFinalidade;
            cboTipoEmissao.SelectedValue = (TipoEmissao)nfe.TipoEmissao;
            txtDataEmissao.Text = nfe.DataEmissao.ToShortDateString();
            rdbDevolucaoNota.IsChecked = true;

            if (nfe.idNFStatus.In((int)EnumStatusNFe.EmitidaComSucesso))
            {
               tbiTotais.IsEnabled = true;
               btnVisualizarDanfe.IsEnabled = true;
               CalcularTotais(nfe);
            }

            tb_nfe_referencia referenciaChave = new tb_nfe_referencia();
            if (nfe.TipoNota == EnumTipoNota.DevolucaoCompra)
            {
               tbiReferenciaDevolucao.Visibility = Visibility.Visible;
               var DevolucaoCompra = new DevolucaoCompraNFeBusiness().BuscarPorIdNFe(nfe.id);
               if (DevolucaoCompra != null)
               {
                  foreach (var dev in DevolucaoCompra.tb_devolucao_compra.tb_devolucao_compra_produto.ToList())
                  {
                     referenciaChave = new tb_nfe_referencia();
                     referenciaChave.Chave = dev.ChaveReferencia;
                     nfeReferenciaObservable.Add(referenciaChave);
                  }  
               }
               else
               {
                  var devolucaoNFe = new NFeReferenciaBusiness().BuscarPorIdNota(nfe.id);
                  if(devolucaoNFe!=null)
                  {
                     foreach (var dev in devolucaoNFe)
                     {
                        referenciaChave = new tb_nfe_referencia();
                        referenciaChave.Chave = dev.Chave;
                        nfeReferenciaObservable.Add(referenciaChave);
                     }
                  }
               }
            }
            else if (nfe.TipoNota == EnumTipoNota.DevolucaoVenda)
            {
               tbiReferenciaDevolucao.Visibility = Visibility.Visible;
               var DevolucaoVenda = new DevolucaoVendaNFeBusiness().BuscarPorIdNFe(nfe.id);
               if (DevolucaoVenda != null)
               {
                  foreach (var dev in DevolucaoVenda.tb_devolucao_venda.tb_devolucao_venda_produto)
                  {
                     referenciaChave = new tb_nfe_referencia();
                     referenciaChave.Chave = dev.ChaveReferencia;
                     nfeReferenciaObservable.Add(referenciaChave);
                  }
               }
               else
               {
                  var devolucaoNFe = new NFeReferenciaBusiness().BuscarPorIdNota(nfe.id);
                  if (devolucaoNFe != null)
                  {
                     foreach (var dev in devolucaoNFe)
                     {
                        referenciaChave = new tb_nfe_referencia();
                        referenciaChave.Chave = dev.Chave;
                        nfeReferenciaObservable.Add(referenciaChave);
                     }
                  }
               }
            }
            if (cboFinalidade.SelectedValue.In(FinalidadeNFe.fnAjuste, FinalidadeNFe.fnComplementar))
            {
               var Nfref = new NFeReferenciaBusiness().BuscarPorIdNota(nfe.id);
               if (Nfref.Count > 0)
               {
                  referenciaChave.Chave = Nfref.FirstOrDefault().Chave;
                  nfeReferenciaObservable.Add(referenciaChave);
               }
            }
            var referenciaContranota = new NFeReferenciaContranotaBusiness().BuscarPorIdNota(nfe.id);
            if (cboTipoNota.SelectedIndex == (int)EnumTipoNota.Entrada && referenciaContranota.Count > 0)
            {
               foreach (tb_nfe_referencia_contranota itemContranota in referenciaContranota)
               {
                  nfeReferenciaContranotaObservable.Add(itemContranota);
               }
               dgContranotaReferencia.ItemsSource = nfeReferenciaContranotaObservable;

            }
            dgChaveReferencia.ItemsSource = nfeReferenciaObservable;
            PreencherDadosProdutoOrigemTelaConsulta(null, null, null, nfe);
            PreencherDadosPagamentoOrigemTelaConsulta(null, null, null, nfe);
            PreencherCamposFrete(null, null, null, nfe);
            PreencherInformacaoComplementar(null, null, null, nfe);
            PreencherErros();
            HabilitarReferencia();
            CarregarCartasCorrecao();
         }

      }
      #endregion

      #region Preencher aba transporte quando dados vier de outras telas
      private void PreencherCamposFrete(tb_nfe_referencia_cupom refCupom = null, tb_devolucao_venda_nfe devolucaoNfe = null, tb_devolucao_compra_nfe devolucaoNfeCompra = null, tb_nfe nfe = null)
      {
         int idNFe = 0;
         if (refCupom != null)
            idNFe = refCupom.idNFe;
         else if (devolucaoNfe != null)
            idNFe = devolucaoNfe.idNFe;
         else if (devolucaoNfeCompra != null)
            idNFe = devolucaoNfeCompra.idNFe;
         else if (nfe != null)
            idNFe = nfe.id;

         var nfFrete = new NFeFreteBusiness().BuscarPorId(idNFe);
         if (nfFrete != null)
         {
            cboTipoFrete.SelectedValue = (ModalidadeFrete)nfFrete.idTipoFrete;
            txtValorFrete.Text = nfFrete.ValorFrete.ToStringOrNull();
            txtQuantidadeVolumesFrete.Text = nfFrete.QuantidadeVolumes.ToStringOrNull();
            txtEspecieVolumes.Text = nfFrete.EspecieVolumes;
            txtMarca.Text = nfFrete.Marca;
            txtNumeracao.Text = nfFrete.Numeracao;
            txtPesoBruto.Text = nfFrete.PesoBruto.ToStringOrNull();
            txtPesoLiquido.Text = nfFrete.PesoLiquido.ToStringOrNull();

            //Groupbox Transportadora
            uscTransportadora.Id = nfFrete.idTransportadora;
            uscMotorista.Id = nfFrete.idMotorista;
            cboPlacaVeiculo.SelectedValue = nfFrete.idVeiculo;
         }
      }
      #endregion

      #region Preencher aba de pagamento quando dados vier de outras telas
      private void PreencherDadosPagamentoOrigemTelaConsulta(tb_nfe_referencia_cupom refCupom = null, tb_devolucao_venda_nfe devolucaoVenda = null, tb_devolucao_compra_nfe devolucaoCompra = null, tb_nfe nfeOrigem = null)
      {
         int idNFe = 0;
         if (refCupom != null)
            idNFe = refCupom.idNFe;
         else if (devolucaoVenda != null)
            idNFe = devolucaoVenda.idNFe;
         else if (devolucaoCompra != null)
            idNFe = devolucaoCompra.idNFe;
         else if (nfeOrigem != null)
            idNFe = nfeOrigem.id;

         var nfe = new NFeBusiness().BuscarPorId(idNFe);
         var dadosPagamento = new NFePagamentoBusiness().BuscarPorIdNFe(idNFe);
         if (nfe != null)
            tbiPagamento.IsEnabled = true;
         foreach (var item in dadosPagamento)
         {
            if (nfe.FormaDePagamento == 0)
               cboFormaPagamento.SelectedValue = IndicadorPagamento.ipVista;
            else if (nfe.FormaDePagamento == 1)
            {
               cboFormaPagamento.SelectedValue = IndicadorPagamento.ipPrazo;
               btnAdicionarParcelas.IsEnabled = true;
            }
            else if (nfe.FormaDePagamento == 2)
               cboFormaPagamento.SelectedValue = IndicadorPagamento.ipOutras;

            tb_nfe_pagamento p = new tb_nfe_pagamento();
            p.Numero = item.Numero;
            p.Valor = item.Valor;
            p.DataVencimento = item.DataVencimento;
            p.idNFe = nfe.id;
            pagamentoObservable.Add(p);
         }
         dgParcelas.ItemsSource = pagamentoObservable;


         var nfeFormaPagamento = new NFeFormaPagamentoBusiness().BuscarPorIdNFe(nfe.id);
         foreach (var item in nfeFormaPagamento)
         {
            tb_nfe_formapagamento f = new tb_nfe_formapagamento();
            var t = new FormaPagamentoBusiness().BuscarPorIdDescricao(item.FormaDePagamento);
            if (t != null)
            {
               f.FormaDePagamento = t.CodFormaPagamentoNFe;
               f.FormaDePagamentoDecricao = t.DescricaoFormaPagamentoNFe;
               f.idNFe = nfe.id;
               decimal dValor = 0;
               dValor = item.ValorDoPagamento.GetValueOrDefault();
               f.ValorDoPagamento = dValor;
               formaPagamentoObservable.Add(f);
            }
         }
         dgFormaPagamento.ItemsSource = formaPagamentoObservable;

         decimal ValorTotalPagamento = nfeProdutoObservable.Sum(x => x.ValorTotalProduto.GetValueOrDefault());
         decimal ValorParcelas = pagamentoObservable.Sum(x => x.Valor);
         txtValorTotalPagamento.Text = ValorTotalPagamento.ToString("N2");
         txtValorRestante.Text = (ValorTotalPagamento - ValorParcelas).ToString("N2");
      }

      private void PreencherDadosPagamento(tb_venda VendaMovimento = null, tb_ordem_servico OrdemMovimento = null, tb_compra CompraMovimento = null, tb_devolucao_compra DevolucaoCompraMovimento = null, tb_devolucao_venda DevolucaoVendaMovimento = null)
      {
         pagamentoObservable.Clear();
         formaPagamentoObservable.Clear();
         idTipoPag = null;
         tbiPagamento.IsEnabled = true;
         int idMovimento = 0;
         if (VendaMovimento != null)
            idMovimento = VendaMovimento.id;
         else if (OrdemMovimento != null)
            idMovimento = OrdemMovimento.idMovimento.GetValueOrDefault();
         else if (CompraMovimento != null)
            idMovimento = CompraMovimento.idMovimento.GetValueOrDefault();

         var existeMovimentoVenda = new VendaMovimentoBusiness().BuscarPorIdVenda(idMovimento);
         var existeMovimento = new MovimentoBusiness().BuscarPorId(idMovimento);

         if (existeMovimentoVenda != null || existeMovimento != null)
         {
            int id = 0;
            if (existeMovimentoVenda != null)
               id = existeMovimentoVenda.idMovimento;
            else if (existeMovimento != null)
               id = existeMovimento.id;

            var movimentoDetalhes = new MovimentoDetalheBusiness().BuscarPorIdMovimento(id);

            if (OrdemMovimento != null)
               txtValorTotalPagamento.Text = OrdemMovimento.ValorNFe;
            else if (VendaMovimento != null)
               txtValorTotalPagamento.Text = VendaMovimento.ValorTotal;

            var categoriaBusiness = new CategoriaMovimentoBusiness();
            var contaFinanceiraBusiness = new ContaFinanceiraBusiness();
            foreach (var item in movimentoDetalhes)
            {
               if (item.idStatusParcela == 1)
               {
                  cboFormaPagamento.SelectedValue = IndicadorPagamento.ipVista;
                  idTipoPag = 0;
               }
               else
               {
                  cboFormaPagamento.SelectedValue = IndicadorPagamento.ipPrazo;
                  idTipoPag = 1;
                  btnAdicionarParcelas.IsEnabled = true;
                  //Carrega as parcelas vindo da tela de venda
                  tb_nfe_pagamento p = new tb_nfe_pagamento();
                  p.Numero = item.NumeroParcela;
                  decimal dValor = 0;
                  if (OrdemMovimento != null)
                     dValor = OrdemMovimento.ValorNFe.ToDecimal() / movimentoDetalhes.Count;//pega o valor da NFe na ordem de servilo e divide pelo a quantidade de parcelas
                  else
                     dValor = item.Valor.GetValueOrDefault();
                  p.Valor = dValor;
                  p.DataVencimento = item.DataVencimento;
                  p.idNFe = nfe.id;
                  pagamentoObservable.Add(p);
               }
               if (parametro.tb_nf_configuracao.Versao == true)
               {
                  if (item.idFormaPagamento == 7 || item.idFormaPagamento == 8)
                  {
                     // Carrega a forma de pagamento vindo da tela de ordem de serviço - valor sem pagamento
                     tb_nfe_formapagamento f = new tb_nfe_formapagamento();
                     var t = new FormaPagamentoBusiness().BuscarPorId(14);
                     f.FormaDePagamento = t.CodFormaPagamentoNFe;
                     f.FormaDePagamentoDecricao = t.DescricaoFormaPagamentoNFe;
                     f.ValorDoPagamento = venda.ValorTotal.ToDecimal();
                     f.idNFe = nfe.id;
                     formaPagamentoObservable.Add(f);
                  }
                  else
                  {
                     // Carrega a forma de pagamento vindo da tela de venda
                     tb_nfe_formapagamento f = new tb_nfe_formapagamento();
                     var t = new FormaPagamentoBusiness().BuscarPorId(item.idFormaPagamento);
                     f.FormaDePagamento = t.CodFormaPagamentoNFe;
                     f.FormaDePagamentoDecricao = t.DescricaoFormaPagamentoNFe;
                     f.idNFe = nfe.id;
                     decimal dValor = 0;
                     if (OrdemMovimento != null)
                        dValor = OrdemMovimento.ValorNFe.ToDecimal() / movimentoDetalhes.Count;//pega o valor da NFe na ordem de servilo e divide pelo a quantidade de parcelas
                     else
                        dValor = item.Valor.GetValueOrDefault();
                     f.ValorDoPagamento = dValor;
                     formaPagamentoObservable.Add(f);
                  }
               }
            }
         }
         else
         {
            cboFormaPagamento.SelectedValue = IndicadorPagamento.ipOutras;
            idTipoPag = 2;
            txtValorTotalPagamento.Text = nfeProdutoObservable.Sum(x => x.ValorTotalProduto.GetValueOrDefault()).ToString("N2");

            if (parametro.tb_nf_configuracao.Versao == true && existeMovimentoVenda != null)
            {
               // Carrega a forma de pagamento vindo da tela de ordem de serviço - valor sem pagamento
               tb_nfe_formapagamento f = new tb_nfe_formapagamento();
               var t = new FormaPagamentoBusiness().BuscarPorId(14);
               f.FormaDePagamento = t.CodFormaPagamentoNFe;
               f.FormaDePagamentoDecricao = t.DescricaoFormaPagamentoNFe;
               f.ValorDoPagamento = venda.ValorTotal.ToDecimal();
               f.idNFe = nfe.id;

               formaPagamentoObservable.Add(f);
            }
         }

         //Grid das parcelas 
         this.dgParcelas.ItemsSource = null;
         this.dgParcelas.ItemsSource = pagamentoObservable;

         if (parametro.tb_nf_configuracao.Versao == true)
         {
            //Grid da forma de pagamento
            this.dgFormaPagamento.ItemsSource = null;
            this.dgFormaPagamento.ItemsSource = formaPagamentoObservable;
         }
         //quando vier sem forma de pagamento
         if (formaPagamentoObservable.Count == 0)
         {
            tb_nfe_formapagamento f = new tb_nfe_formapagamento();
            var t = new FormaPagamentoBusiness().BuscarPorId(14);
            f.FormaDePagamento = t.CodFormaPagamentoNFe;
            f.FormaDePagamentoDecricao = t.DescricaoFormaPagamentoNFe;
            if (VendaMovimento != null)
               f.ValorDoPagamento = VendaMovimento.ValorTotal.ToDecimal();
            if (CompraMovimento != null)
               f.ValorDoPagamento = CompraMovimento.ValorTotalCompra;
            if (OrdemMovimento != null)
               f.ValorDoPagamento = OrdemMovimento.ValorNFe.ToDecimal();
            if (DevolucaoCompraMovimento != null)
               f.ValorDoPagamento = DevolucaoCompraMovimento.tb_devolucao_compra_produto.Sum(x => x.ValorFinalProduto);
            if (DevolucaoVendaMovimento != null)
               f.ValorDoPagamento = DevolucaoVendaMovimento.tb_devolucao_venda_produto.Sum(x => x.ValorUnitario * x.Quantidade);
            f.idNFe = nfe.id;
            formaPagamentoObservable.Add(f);
         }
         decimal ValorTotalPagamento = nfeProdutoObservable.Sum(x => x.ValorTotalProduto.GetValueOrDefault());
         decimal ValorParcelas = pagamentoObservable.Sum(x => x.Valor);
         txtValorTotalPagamento.Text = ValorTotalPagamento.ToString("N2");
         txtValorRestante.Text = (ValorTotalPagamento - ValorParcelas).ToString("N2");
      }
      #endregion

      #region Preencher aba de produto quando dados vier de outras telas

      private void PreencherDadosProdutoCupom(tb_nfe_referencia_cupom refCupom)
      {

      }
      private void PreencherDadosProdutoOrdemServico(object DadosProduto)
      {
         decimal valorTotal = 0;
         if (DadosProduto is List<tb_ordem_servico_produto>)
            DadosProduto = DadosProduto as List<tb_ordem_servico_produto>;

         foreach (var item in (dynamic)DadosProduto)
         {
            var produtoOrdem = item as tb_ordem_servico_produto;
            var nfeProduto = new tb_nfe_produto();
            if (nfe != null && nfe.id > 0)
               nfeProduto.idNFe = nfe.id;
            nfeProduto.idProduto = produtoOrdem.idProduto;

            nfeProduto.Quantidade = produtoOrdem.Quantidade.ToInt32();
            nfeProduto.ValorUnitario = produtoOrdem.ValorUnitario;
            nfeProduto.ValorDesconto = produtoOrdem.ValorDesconto;

            nfeProduto.tb_produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);

            var produto = nfeProduto.tb_produto;
            nfeProduto.tb_produto.idUnidade = produtoOrdem.tb_produto.idUnidade;
            if (produtoOrdem.tb_produto.idUnidade > 0)
               nfeProduto.tb_produto.tb_unidade = new UnidadeBusiness().BuscarPorId(produtoOrdem.tb_produto.idUnidade);

            var cliente = clienteBusiness.BuscarPorId(uscClienteDestinatario.Id.Value);
            int idCliente = 0;
            idCliente = produtoOrdem.tb_ordem_servico.idCliente.GetValueOrDefault();

            var estadoCliente = clienteBusiness.BuscarPorId(idCliente).tb_cidade.tb_estado;
            if ((empresa.tb_cidade.idEstado != estadoCliente.id
              && empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id) && produto.UtilizarTabelaICMS && produto.idTabelaICMS.HasValue)
            {
               this.aliquotaIntraestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoOrigem == cliente.tb_cidade.idEstado && x.idEstadoDestino == cliente.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
               this.aliquotaInterestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoDestino == cliente.tb_cidade.idEstado && x.idEstadoOrigem == empresa.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
            }
            else if (empresa.tb_cidade.idEstado != estadoCliente.id)
            {
               var lista = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(nfeProduto.idProduto);
               if (lista != null && lista.Count > 0)
               {
                  var existeEstado = lista.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                  if (existeEstado != null)
                  {
                     this.aliquotaInterestadual = existeEstado.Aliquota;
                  }
               }
            }

            if (empresa.tb_cidade.idEstado == estadoCliente.id)
               nfeProduto.idCFOP = produto.idCFOPEstadual;
            else if (empresa.tb_cidade.idEstado != estadoCliente.id && empresa.tb_cidade.tb_estado.idPais == estadoCliente.idPais)
               nfeProduto.idCFOP = produto.idCFOPInterestadual;
            else
               nfeProduto.idCFOP = produto.idCFOPExterior;
            if (produtoOrdem.Bonificacao)
            {
               if (empresa.tb_cidade.idEstado == estadoCliente.id)
                  nfeProduto.idCFOP = 5910;
               else if (empresa.tb_cidade.idEstado != estadoCliente.id && empresa.tb_cidade.tb_estado.idPais == estadoCliente.idPais)
                  nfeProduto.idCFOP = 6910;
               else
                  nfeProduto.idCFOP = 7910;
            }
            // Interestadual
            var icmsInterestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(produto.id);
            nfeProduto.idOrigemMercadoria = produto.tb_produto_imposto.idOrigemMercadoria;
            if (empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id)
            {
               if (new ParametroBusiness().BuscarParametroVigente().idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
               {
                  nfeProduto.idCSOSN = produto.tb_produto_imposto.idCSOSN;
                  uscCSTICMS.Id = nfeProduto.idCSOSN;
                  nfeProduto.ValorAliquotaICMS = produtoOrdem.AliquotaICMS;
                  uscCSTICMS.Tag = "CSOSN";
               }
               else
               {
                  produto.tb_produto_icms_interestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(produto.id);
                  if (empresa.tb_cidade.idEstado != estadoCliente.id && produto.tb_produto_icms_interestadual != null && produto.tb_produto_icms_interestadual.Any() && !produto.UtilizarTabelaICMS)
                  {
                     var existeEstado = produto.tb_produto_icms_interestadual.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                     if (existeEstado != null)
                     {
                        nfeProduto.idCSTICMS = existeEstado.idCST;
                        nfeProduto.idOrigemMercadoria = existeEstado.idOrigem;
                        nfeProduto.MVA = existeEstado.MVA;
                        nfeProduto.PorcentagemReducao = existeEstado.PorcentagemReducao;

                        if (nfeProduto.idCSTICMS != 10)
                           nfeProduto.ValorAliquotaICMS = existeEstado.Aliquota;
                        else
                           nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                        {
                           nfeProduto.MVA = existeEstado.MVA;
                           nfeProduto.PorcentagemReducaoST = existeEstado.PorcentagemReducao;
                           nfeProduto.ValorAliquotaST = existeEstado.Aliquota;
                        }
                     }
                     else
                     {
                        nfeProduto.idCSTICMS = produto.tb_produto_imposto.idCSTICMS;
                        nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                           nfeProduto.MVA = produto.tb_produto_imposto.MVA;
                     }
                  }
                  else
                  {
                     nfeProduto.PorcentagemReducao = produto.tb_produto_imposto.PorcentagemReducao;
                     nfeProduto.idCSTICMS = produto.tb_produto_imposto.idCSTICMS;
                     nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS.ToInt32();
                     nfeProduto.ValorIPI = produto.tb_produto_imposto.AliquotaIPISaida.GetValueOrDefault();
                     if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                        nfeProduto.MVA = produto.tb_produto_imposto.MVA.ToInt32();
                  }
                  uscCSTICMS.Tag = "CST";
               }
            }
            nfeProduto.PorcentagemReducao = produtoOrdem.PorcentagemReducao;
            nfeProduto.PorcentagemReducaoST = produtoOrdem.PorcentagemReducao;
            //VALORES ICMS
            nfeProduto.ValorCreditoICMS = produtoOrdem.ValorICMS;
            nfeProduto.ValorICMS = produtoOrdem.ValorICMS;
            nfeProduto.ValorBaseCalculoICMS = produtoOrdem.ValorBaseCalculoICMS;
            //VALORES ICMS ST
            if (produto.tb_produto_imposto.idCSTICMS.In(10, 70, 90) || produto.tb_produto_imposto.idCSOSN.In(201, 202, 203, 900))
            {
               if (produtoOrdem.AliquotaICMS > 0)
               {
                  nfeProduto.ValorBaseCalculoICMSST = produtoOrdem.ValorBaseCalculoICMSST;
                  nfeProduto.MVA = produtoOrdem.MVA;
                  if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
                     nfeProduto.ValorAliquotaST = produtoOrdem.AliquotaST;
                  else
                     nfeProduto.ValorAliquotaST = produtoOrdem.AliquotaICMS;
                  nfeProduto.ValorICMSST = produtoOrdem.ValorICMSST;
               }
            }
            //VALOR TOTAL PRDUTO
            nfeProduto.ValorTotalProduto = produtoOrdem.ValorTotal;
            //PIS
            nfeProduto.idCSTPIS = produto.tb_produto_imposto.idCSTPISCOFINS;
            nfeProduto.ValorBaseCalculoPIS = produtoOrdem.ValorUnitario * produtoOrdem.Quantidade - produtoOrdem.ValorDesconto.GetValueOrDefault();
            nfeProduto.ValorAliquotaPIS = produtoOrdem.AliquotaPIS;
            nfeProduto.ValorPIS = produtoOrdem.ValorPIS;
            //COFINS
            nfeProduto.idCSTCOFINS = produto.tb_produto_imposto.idCSTPISCOFINS;
            nfeProduto.ValorBaseCalculoCOFINS = produtoOrdem.ValorUnitario * produtoOrdem.Quantidade - produtoOrdem.ValorDesconto.GetValueOrDefault();
            nfeProduto.ValorAliquotaCOFINS = produtoOrdem.AliquotaCOFINS;
            nfeProduto.ValorCOFINS = produtoOrdem.ValorCOFINS;
            //IPI
            nfeProduto.idCSTIPI = produto.tb_produto_imposto.idCSTIPISaida;
            nfeProduto.ValorBaseCalculoIPI = produtoOrdem.ValorUnitario * produtoOrdem.Quantidade - produtoOrdem.ValorDesconto.GetValueOrDefault();
            nfeProduto.ValorAliquotaIPI = produtoOrdem.AliquotaIPISaida;
            nfeProduto.ValorIPI = produtoOrdem.ValorIPI;

            nfeProdutoObservable.Add(nfeProduto);

            //partilhaICMS
            decimal baseCalculoPartilha = 0;
            baseCalculoPartilha = (produtoOrdem.ValorUnitario * produtoOrdem.Quantidade - produtoOrdem.ValorDesconto.GetValueOrDefault());
            CalcularPartilhaICMS(baseCalculoPartilha, produto, nfeProduto);
            //

            //Contar Produtos
            int contador = 1;
            int? numeroItem = 0;

            foreach (tb_nfe_produto itemProduto in nfeProdutoObservable)
            {
               itemProduto.NumeroItem = contador;
               contador++;

               if (nfeProduto.NumeroItem == 1)
                  numeroItem = item.NumeroItem;
            }
            //setar a natureza de operação
            if (string.IsNullOrEmpty(txtNaturezaOperacao.Text) || numeroItem == 1)
            {
               var cfop = new CFOPBusiness().BuscarPorId(nfeProdutoObservable.FirstOrDefault().idCFOP.Value);
               txtNaturezaOperacao.Text = cfop.Descricao;
            }
            dgProdutos.ItemsSource = nfeProdutoObservable;

            valorTotal += produtoOrdem.ValorTotal;
         }
         txtValorTotalNota.Text = valorTotal.ToString("N2");
      }
      private void PreencherDadosProduto(object DadosProduto)
      {
         decimal valorTotal = 0;

         if (DadosProduto is List<tb_venda_produto>)
            DadosProduto = DadosProduto as List<tb_venda_produto>;

         nfeProdutoObservable.Clear();
         foreach (var item in (dynamic)DadosProduto)
         {
            var vendaProduto = item as tb_venda_produto;

            var nfeProduto = new tb_nfe_produto();
            if (nfe != null && nfe.id > 0)
               nfeProduto.idNFe = nfe.id;
            nfeProduto.idProduto = vendaProduto.idProduto;

            if (vendaProduto is tb_venda_produto)
               nfeProduto.ComplementoDescricao = (vendaProduto as tb_venda_produto).ComplementoDescricao;

            nfeProduto.Quantidade = vendaProduto.Quantidade.ToInt32();
            nfeProduto.ValorUnitario = vendaProduto.ValorUnitario;
            nfeProduto.ValorDesconto = vendaProduto.ValorDesconto;

            nfeProduto.tb_produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);

            var produto = nfeProduto.tb_produto;
            nfeProduto.tb_produto.idUnidade = vendaProduto.tb_produto.idUnidade;
            if (vendaProduto.tb_produto.idUnidade > 0)
               nfeProduto.tb_produto.tb_unidade = new UnidadeBusiness().BuscarPorId(vendaProduto.tb_produto.idUnidade);

            var cliente = clienteBusiness.BuscarPorId(uscClienteDestinatario.Id.Value);
            int idCliente = 0;
            if (DadosProduto is List<tb_venda_produto>)
               idCliente = (vendaProduto as tb_venda_produto).tb_venda.idCliente;

            var estadoCliente = clienteBusiness.BuscarPorId(idCliente).tb_cidade.tb_estado;
            if ((empresa.tb_cidade.idEstado != estadoCliente.id
              && empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id) && produto.UtilizarTabelaICMS && produto.idTabelaICMS.HasValue)
            {
               this.aliquotaIntraestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoOrigem == cliente.tb_cidade.idEstado && x.idEstadoDestino == cliente.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
               this.aliquotaInterestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoDestino == cliente.tb_cidade.idEstado && x.idEstadoOrigem == empresa.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
            }
            else if (empresa.tb_cidade.idEstado != estadoCliente.id)
            {
               var lista = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(nfeProduto.idProduto);
               if (lista != null && lista.Count > 0)
               {
                  var existeEstado = lista.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                  if (existeEstado != null)
                  {
                     this.aliquotaInterestadual = existeEstado.Aliquota;
                  }
               }
            }

            if (empresa.tb_cidade.idEstado == estadoCliente.id)
               nfeProduto.idCFOP = produto.idCFOPEstadual;
            else if (empresa.tb_cidade.idEstado != estadoCliente.id && empresa.tb_cidade.tb_estado.idPais == estadoCliente.idPais)
               nfeProduto.idCFOP = produto.idCFOPInterestadual;
            else
               nfeProduto.idCFOP = produto.idCFOPExterior;
            if (vendaProduto.Bonificacao)
            {
               if (empresa.tb_cidade.idEstado == estadoCliente.id)
                  nfeProduto.idCFOP = 5910;
               else if (empresa.tb_cidade.idEstado != estadoCliente.id && empresa.tb_cidade.tb_estado.idPais == estadoCliente.idPais)
                  nfeProduto.idCFOP = 6910;
               else
                  nfeProduto.idCFOP = 7910;
            }

            // Interestadual
            var icmsInterestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(produto.id);
            //Verifica se o ICMS do cliente é igual a um icms interestadual, se for usa os valores que foram cadastrados.
            if (icmsInterestadual != null && icmsInterestadual.Count > 0 && icmsInterestadual.Where(x => x.idEstado == estadoCliente.id).Any())
            {
               // Para ICMS
               produto.tb_produto_imposto.idOrigemMercadoria = icmsInterestadual.FirstOrDefault().idOrigem;
               produto.tb_produto_imposto.AliquotaICMS = icmsInterestadual.FirstOrDefault().Aliquota;
               produto.tb_produto_imposto.PorcentagemReducao = icmsInterestadual.FirstOrDefault().PorcentagemReducao.GetValueOrDefault();
               produto.tb_produto_imposto.PorcentagemDiferimento = icmsInterestadual.FirstOrDefault().PorcentagemDiferimento.GetValueOrDefault();
               produto.tb_produto_imposto.MVA = icmsInterestadual.FirstOrDefault().MVA.GetValueOrDefault();
               if (icmsInterestadual.FirstOrDefault().idCST.HasValue)
                  produto.tb_produto_imposto.idCSTICMS = icmsInterestadual.FirstOrDefault().idCST.Value;
               else if (icmsInterestadual.FirstOrDefault().idCSOSN.HasValue)
                  produto.tb_produto_imposto.idCSOSN = icmsInterestadual.FirstOrDefault().idCSOSN.Value;

               // Para ICMSST
               nfeProduto.PorcentagemReducao = icmsInterestadual.FirstOrDefault().PorcentagemReducao.GetValueOrDefault();
               nfeProduto.MVA = icmsInterestadual.FirstOrDefault().MVA.GetValueOrDefault();
               produto.tb_produto_imposto.AliquotaST = icmsInterestadual.FirstOrDefault().Aliquota;
            }
            //ICMS
            nfeProduto.idOrigemMercadoria = produto.tb_produto_imposto.idOrigemMercadoria;
            if (empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id)
            {
               if (new ParametroBusiness().BuscarParametroVigente().idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
               {
                  nfeProduto.idCSOSN = produto.tb_produto_imposto.idCSOSN;
                  uscCSTICMS.Id = nfeProduto.idCSOSN;
                  nfeProduto.ValorAliquotaICMS = vendaProduto.AliquotaICMS;
                  uscCSTICMS.Tag = "CSOSN";
               }
               else
               {
                  produto.tb_produto_icms_interestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(produto.id);
                  if (empresa.tb_cidade.idEstado != estadoCliente.id && produto.tb_produto_icms_interestadual != null && produto.tb_produto_icms_interestadual.Any() && !produto.UtilizarTabelaICMS)
                  {
                     var existeEstado = produto.tb_produto_icms_interestadual.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                     if (existeEstado != null)
                     {
                        nfeProduto.idCSTICMS = existeEstado.idCST;
                        nfeProduto.idOrigemMercadoria = existeEstado.idOrigem;
                        nfeProduto.MVA = existeEstado.MVA;
                        nfeProduto.PorcentagemReducao = existeEstado.PorcentagemReducao;
                        nfeProduto.PorcentagemDiferimento = existeEstado.PorcentagemDiferimento;

                        if (nfeProduto.idCSTICMS != 10)
                           nfeProduto.ValorAliquotaICMS = existeEstado.Aliquota;
                        else
                           nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                        {
                           nfeProduto.MVA = existeEstado.MVA;
                           nfeProduto.PorcentagemReducaoST = existeEstado.PorcentagemReducao;
                           nfeProduto.ValorAliquotaST = existeEstado.Aliquota;
                        }
                     }
                     else
                     {
                        nfeProduto.idCSTICMS = produto.tb_produto_imposto.idCSTICMS;
                        nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                           nfeProduto.MVA = produto.tb_produto_imposto.MVA;
                     }
                  }
                  else
                  {
                     nfeProduto.PorcentagemDiferimento = produto.tb_produto_imposto.PorcentagemDiferimento;
                     nfeProduto.PorcentagemReducao = produto.tb_produto_imposto.PorcentagemReducao;
                     nfeProduto.idCSTICMS = produto.tb_produto_imposto.idCSTICMS;
                     nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS.ToInt32();
                     nfeProduto.ValorIPI = produto.tb_produto_imposto.AliquotaIPISaida.GetValueOrDefault();
                     if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                        nfeProduto.MVA = produto.tb_produto_imposto.MVA.ToInt32();
                  }

                  uscCSTICMS.Tag = "CST";
               }
            }

            nfeProduto.PorcentagemReducao = vendaProduto.PorcentagemReducao;
            nfeProduto.PorcentagemReducaoST = vendaProduto.PorcentagemReducao;

            //VALORES ICMS
            nfeProduto.ValorCreditoICMS = vendaProduto.ValorICMS;
            nfeProduto.ValorICMS = vendaProduto.ValorICMS;
            nfeProduto.ValorBaseCalculoICMS = vendaProduto.ValorBaseCalculoICMS;

            //VALORES ICMS ST
            if (produto.tb_produto_imposto.idCSTICMS.In(10, 70, 90) || produto.tb_produto_imposto.idCSOSN.In(201, 202, 203, 900))
            {
               if (vendaProduto.AliquotaICMS > 0)
               {
                  nfeProduto.ValorBaseCalculoICMSST = vendaProduto.ValorBaseCalculoICMSST;
                  nfeProduto.MVA = vendaProduto.MVA;
                  if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
                     nfeProduto.ValorAliquotaST = vendaProduto.AliquotaST;
                  else
                     nfeProduto.ValorAliquotaST = vendaProduto.AliquotaICMS;
                  nfeProduto.ValorICMSST = vendaProduto.ValorICMSST;
               }
            }

            //VALOR TOTAL PRDUTO
            nfeProduto.ValorTotalProduto = vendaProduto.ValorTotal;

            //PIS
            nfeProduto.idCSTPIS = produto.tb_produto_imposto.idCSTPISCOFINS;
            nfeProduto.ValorBaseCalculoPIS = vendaProduto.ValorUnitario * vendaProduto.Quantidade - vendaProduto.ValorDesconto.GetValueOrDefault();
            nfeProduto.ValorAliquotaPIS = vendaProduto.AliquotaPIS;
            nfeProduto.ValorPIS = vendaProduto.ValorPIS;
            //COFINS
            nfeProduto.idCSTCOFINS = produto.tb_produto_imposto.idCSTPISCOFINS;
            nfeProduto.ValorBaseCalculoCOFINS = vendaProduto.ValorUnitario * vendaProduto.Quantidade - vendaProduto.ValorDesconto.GetValueOrDefault();
            nfeProduto.ValorAliquotaCOFINS = vendaProduto.AliquotaCOFINS;
            nfeProduto.ValorCOFINS = vendaProduto.ValorCOFINS;
            //IPI
            nfeProduto.idCSTIPI = produto.tb_produto_imposto.idCSTIPISaida;
            nfeProduto.ValorBaseCalculoIPI = vendaProduto.ValorUnitario * vendaProduto.Quantidade - vendaProduto.ValorDesconto.GetValueOrDefault();
            nfeProduto.ValorAliquotaIPI = vendaProduto.AliquotaIPISaida;
            nfeProduto.ValorIPI = vendaProduto.ValorIPI;

            nfeProdutoObservable.Add(nfeProduto);

            //Contar Produtos
            int contador = 1;
            int? numeroItem = 0;

            foreach (tb_nfe_produto itemProduto in nfeProdutoObservable)
            {
               itemProduto.NumeroItem = contador;
               contador++;

               if (nfeProduto.NumeroItem == 1)
                  numeroItem = item.NumeroItem;
            }
            //setar a natureza de operação
            if (string.IsNullOrEmpty(txtNaturezaOperacao.Text) || numeroItem == 1)
            {
               var cfop = new CFOPBusiness().BuscarPorId(nfeProdutoObservable.FirstOrDefault().idCFOP.Value);
               txtNaturezaOperacao.Text = cfop.Descricao;
            }
            dgProdutos.ItemsSource = nfeProdutoObservable;

            //partilhaICMS
            decimal baseCalculoPartilha = 0;
            baseCalculoPartilha = (vendaProduto.ValorUnitario * vendaProduto.Quantidade - vendaProduto.ValorDesconto.GetValueOrDefault());
            CalcularPartilhaICMS(baseCalculoPartilha, produto, nfeProduto);
            //

            valorTotal += vendaProduto.ValorTotal;
         }
         txtValorTotalNota.Text = valorTotal.ToString("N2");
      }
      private void PreencherDadosProdutoCompra(object DadosProduto)
      {
         decimal valorTotal = 0;
         if (DadosProduto is List<tb_compra_produto>)
            DadosProduto = DadosProduto as List<tb_compra_produto>;

         foreach (var item in (dynamic)DadosProduto)
         {
            var produtoCompra = item as tb_compra_produto;
            var nfeProduto = new tb_nfe_produto();
            if (nfe != null && nfe.id > 0)
               nfeProduto.idNFe = nfe.id;
            nfeProduto.idProduto = produtoCompra.idProduto;

            nfeProduto.Quantidade = produtoCompra.Quantidade.ToInt32();
            nfeProduto.ValorUnitario = produtoCompra.ValorUnitario;
            nfeProduto.ValorDesconto = produtoCompra.ValorDesconto;
            nfeProduto.ValorFrete = produtoCompra.ValorFrete;
            nfeProduto.ValorOutrasDespesas = produtoCompra.ValorOutrasDespesas;
            nfeProduto.ValorSeguro = produtoCompra.ValorSeguro;

            nfeProduto.tb_produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);

            var produto = nfeProduto.tb_produto;
            nfeProduto.tb_produto.idUnidade = produtoCompra.tb_produto.idUnidade;
            if (produtoCompra.tb_produto.idUnidade > 0)
               nfeProduto.tb_produto.tb_unidade = new UnidadeBusiness().BuscarPorId(produtoCompra.tb_produto.idUnidade);

            int idCliente = 0;
            idCliente = produtoCompra.tb_compra.idFornecedor;

            var estadoCliente = new FornecedorBusiness().BuscarPorId(idCliente).tb_cidade.tb_estado;
            if ((empresa.tb_cidade.idEstado != estadoCliente.id
              && empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id) && produto.UtilizarTabelaICMS && produto.idTabelaICMS.HasValue)
            {
               this.aliquotaIntraestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoOrigem == cliente.tb_cidade.idEstado && x.idEstadoDestino == cliente.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
               this.aliquotaInterestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoDestino == cliente.tb_cidade.idEstado && x.idEstadoOrigem == empresa.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
            }
            else if (empresa.tb_cidade.idEstado != estadoCliente.id)
            {
               var lista = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(nfeProduto.idProduto);
               if (lista != null && lista.Count > 0)
               {
                  var existeEstado = lista.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                  if (existeEstado != null)
                  {
                     this.aliquotaInterestadual = existeEstado.Aliquota;
                  }
               }
            }

            if (empresa.tb_cidade.idEstado == estadoCliente.id)
               nfeProduto.idCFOP = produto.idCFOPEstadualEntrada;
            else if (empresa.tb_cidade.idEstado != estadoCliente.id && empresa.tb_cidade.tb_estado.idPais == estadoCliente.idPais)
               nfeProduto.idCFOP = produto.idCFOPInterestadual;
            else
               nfeProduto.idCFOP = produto.idCFOPExterior;
            // Interestadual
            var icmsInterestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(produto.id);
            //Verifica se o ICMS do cliente é igual a um icms interestadual, se for usa os valores que foram cadastrados.
            if (icmsInterestadual != null && icmsInterestadual.Count > 0 && icmsInterestadual.Where(x => x.idEstado == estadoCliente.id).Any())
            {
               // Para ICMS
               produto.tb_produto_imposto.idOrigemMercadoria = icmsInterestadual.FirstOrDefault().idOrigem;
               produto.tb_produto_imposto.AliquotaICMS = icmsInterestadual.FirstOrDefault().Aliquota;
               produto.tb_produto_imposto.PorcentagemReducao = icmsInterestadual.FirstOrDefault().PorcentagemReducao.GetValueOrDefault();
               produto.tb_produto_imposto.PorcentagemDiferimento = icmsInterestadual.FirstOrDefault().PorcentagemDiferimento.GetValueOrDefault();
               produto.tb_produto_imposto.MVA = icmsInterestadual.FirstOrDefault().MVA.GetValueOrDefault();
               if (icmsInterestadual.FirstOrDefault().idCST.HasValue)
                  produto.tb_produto_imposto.idCSTICMS = icmsInterestadual.FirstOrDefault().idCST.Value;
               else if (icmsInterestadual.FirstOrDefault().idCSOSN.HasValue)
                  produto.tb_produto_imposto.idCSOSN = icmsInterestadual.FirstOrDefault().idCSOSN.Value;

               // Para ICMSST
               nfeProduto.PorcentagemReducao = icmsInterestadual.FirstOrDefault().PorcentagemReducao.GetValueOrDefault();
               nfeProduto.MVA = icmsInterestadual.FirstOrDefault().MVA.GetValueOrDefault();
               produto.tb_produto_imposto.AliquotaST = icmsInterestadual.FirstOrDefault().Aliquota;
            }
            //ICMS
            nfeProduto.idOrigemMercadoria = produto.tb_produto_imposto.idOrigemMercadoria;
            if (empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id)
            {
               if (new ParametroBusiness().BuscarParametroVigente().idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
               {
                  nfeProduto.idCSOSN = produto.tb_produto_imposto.idCSOSN;
                  uscCSTICMS.Id = nfeProduto.idCSOSN;
                  nfeProduto.ValorAliquotaICMS = produtoCompra.ValorAliquotaICMS;
                  uscCSTICMS.Tag = "CSOSN";
               }
               else
               {
                  produto.tb_produto_icms_interestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(produto.id);
                  if (empresa.tb_cidade.idEstado != estadoCliente.id && produto.tb_produto_icms_interestadual != null && produto.tb_produto_icms_interestadual.Any() && !produto.UtilizarTabelaICMS)
                  {
                     var existeEstado = produto.tb_produto_icms_interestadual.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                     if (existeEstado != null)
                     {
                        nfeProduto.idCSTICMS = existeEstado.idCST;
                        nfeProduto.idOrigemMercadoria = existeEstado.idOrigem;
                        nfeProduto.MVA = existeEstado.MVA;
                        nfeProduto.PorcentagemReducao = existeEstado.PorcentagemReducao;

                        if (nfeProduto.idCSTICMS != 10)
                           nfeProduto.ValorAliquotaICMS = existeEstado.Aliquota;
                        else
                           nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                        {
                           nfeProduto.MVA = existeEstado.MVA;
                           nfeProduto.PorcentagemReducaoST = existeEstado.PorcentagemReducao;
                           nfeProduto.ValorAliquotaST = existeEstado.Aliquota;
                        }
                     }
                     else
                     {
                        nfeProduto.idCSTICMS = produto.tb_produto_imposto.idCSTICMS;
                        nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                           nfeProduto.MVA = produto.tb_produto_imposto.MVA;
                     }
                  }

                  uscCSTICMS.Tag = "CST";
               }
            }
            nfeProduto.PorcentagemReducao = produtoCompra.PorcentagemReducaoICMS;
            nfeProduto.PorcentagemReducaoST = produtoCompra.PorcentagemReducaoICMSST;
            //VALORES ICMS
            nfeProduto.idCSTICMS = produtoCompra.idCSTICMS;
            nfeProduto.ValorCreditoICMS = produtoCompra.ValorICMS;
            nfeProduto.ValorICMS = produtoCompra.ValorICMS;
            nfeProduto.ValorBaseCalculoICMS = produtoCompra.ValorBaseCalculoICMS;
            nfeProduto.ValorAliquotaICMS = produtoCompra.ValorAliquotaICMS;
            //VALORES ICMS ST
            if (produto.tb_produto_imposto.idCSTICMS.In(10, 70, 90) || produto.tb_produto_imposto.idCSOSN.In(201, 202, 203, 900))
            {
               if (produtoCompra.ValorAliquotaICMS > 0)
               {
                  nfeProduto.ValorBaseCalculoICMSST = produtoCompra.ValorBaseCalculoICMS;
                  nfeProduto.MVA = produtoCompra.ValorMVA;
                  if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
                     nfeProduto.ValorAliquotaST = produtoCompra.ValorAliquotaST;
                  else
                     nfeProduto.ValorAliquotaST = produtoCompra.ValorAliquotaICMS;
                  nfeProduto.ValorICMSST = produtoCompra.ValorICMSST;
               }
            }
            //VALOR TOTAL PRDUTO
            nfeProduto.ValorTotalProduto = produtoCompra.ValorTotal;
            //PIS
            nfeProduto.idCSTPIS = produto.tb_produto_imposto.idCSTPISCOFINS;
            nfeProduto.ValorBaseCalculoPIS = produtoCompra.ValorUnitario * produtoCompra.Quantidade - produtoCompra.ValorDesconto.GetValueOrDefault();
            nfeProduto.ValorAliquotaPIS = produtoCompra.ValorAliquotaPIS;
            nfeProduto.ValorPIS = produtoCompra.ValorPIS;
            //COFINS
            nfeProduto.idCSTCOFINS = produto.tb_produto_imposto.idCSTPISCOFINS;
            nfeProduto.ValorBaseCalculoCOFINS = produtoCompra.ValorUnitario * produtoCompra.Quantidade - produtoCompra.ValorDesconto.GetValueOrDefault();
            nfeProduto.ValorAliquotaCOFINS = produtoCompra.ValorAliquotaCOFINS;
            nfeProduto.ValorCOFINS = produtoCompra.ValorCOFINS;
            //IPI
            if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional && parametro.Industrial)
            {
               nfeProduto.idCSTIPI = produto.tb_produto_imposto.idCSTIPISaida;
               nfeProduto.ValorBaseCalculoIPI = produtoCompra.ValorUnitario * produtoCompra.Quantidade - produtoCompra.ValorDesconto.GetValueOrDefault();
               nfeProduto.ValorAliquotaIPI = produtoCompra.ValorAliquotaIPI;
               nfeProduto.ValorIPI = produtoCompra.ValorIPI;
            }
            nfeProdutoObservable.Add(nfeProduto);

            //partilhaICMS
            decimal baseCalculoPartilha = 0;
            baseCalculoPartilha = (produtoCompra.ValorUnitario * produtoCompra.Quantidade - produtoCompra.ValorDesconto.GetValueOrDefault());
            CalcularPartilhaICMS(baseCalculoPartilha, produto, nfeProduto);
            //

            //Contar Produtos
            int contador = 1;
            int? numeroItem = 0;

            foreach (tb_nfe_produto itemProduto in nfeProdutoObservable)
            {
               itemProduto.NumeroItem = contador;
               contador++;

               if (nfeProduto.NumeroItem == 1)
                  numeroItem = item.NumeroItem;
            }
            //setar a natureza de operação
            if (string.IsNullOrEmpty(txtNaturezaOperacao.Text) || numeroItem == 1)
            {
               var cfop = new CFOPBusiness().BuscarPorId(nfeProdutoObservable.FirstOrDefault().idCFOP.Value);
               txtNaturezaOperacao.Text = cfop.Descricao;
            }
            dgProdutos.ItemsSource = nfeProdutoObservable;

            valorTotal += produtoCompra.ValorTotal;
         }
         txtValorTotalNota.Text = valorTotal.ToString("N2");
      }
      private void PreencherDadosProdutoDevolucaoCompra(object DadosProduto)
      {
         decimal valorTotal = 0;
         if (DadosProduto is List<tb_devolucao_compra_produto>)
            DadosProduto = DadosProduto as List<tb_devolucao_compra_produto>;

         foreach (var item in (dynamic)DadosProduto)
         {
            var produtoDevolucao = item as tb_devolucao_compra_produto;
            var nfeProduto = new tb_nfe_produto();
            if (nfe != null && nfe.id > 0)
               nfeProduto.idNFe = nfe.id;
            nfeProduto.idProduto = produtoDevolucao.idProduto;

            nfeProduto.Quantidade = produtoDevolucao.Quantidade.ToInt32();
            nfeProduto.ValorUnitario = produtoDevolucao.ValorUnitario;

            nfeProduto.tb_produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);

            var produto = nfeProduto.tb_produto;
            nfeProduto.tb_produto.idUnidade = produtoDevolucao.tb_produto.idUnidade;
            if (produtoDevolucao.tb_produto.idUnidade > 0)
               nfeProduto.tb_produto.tb_unidade = new UnidadeBusiness().BuscarPorId(produtoDevolucao.tb_produto.idUnidade);

            var cliente = clienteBusiness.BuscarPorId(uscClienteDestinatario.Id.Value);
            int idCliente = 0;
            idCliente = produtoDevolucao.tb_devolucao_compra.idCliente;

            var estadoCliente = clienteBusiness.BuscarPorId(idCliente).tb_cidade.tb_estado;
            if ((empresa.tb_cidade.idEstado != estadoCliente.id
              && empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id) && produto.UtilizarTabelaICMS && produto.idTabelaICMS.HasValue)
            {
               this.aliquotaIntraestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoOrigem == cliente.tb_cidade.idEstado && x.idEstadoDestino == cliente.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
               this.aliquotaInterestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoDestino == cliente.tb_cidade.idEstado && x.idEstadoOrigem == empresa.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
            }
            else if (empresa.tb_cidade.idEstado != estadoCliente.id)
            {
               var lista = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(nfeProduto.idProduto);
               if (lista != null && lista.Count > 0)
               {
                  var existeEstado = lista.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                  if (existeEstado != null)
                  {
                     this.aliquotaInterestadual = existeEstado.Aliquota;
                  }
               }
            }

            nfeProduto.idCFOP = produtoDevolucao.idCFOP;
            nfeProduto.idOrigemMercadoria = produto.tb_produto_imposto.idOrigemMercadoria;
            if (empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id)
            {
               if (new ParametroBusiness().BuscarParametroVigente().idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
               {
                  nfeProduto.idCSOSN = produto.tb_produto_imposto.idCSOSN;
                  uscCSTICMS.Id = nfeProduto.idCSOSN;
                  nfeProduto.ValorAliquotaICMS = produtoDevolucao.AliquotaICMS;
                  uscCSTICMS.Tag = "CSOSN";
               }
               else
               {
                  produto.tb_produto_icms_interestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(produto.id);
                  if (empresa.tb_cidade.idEstado != estadoCliente.id && produto.tb_produto_icms_interestadual != null && produto.tb_produto_icms_interestadual.Any() && !produto.UtilizarTabelaICMS)
                  {
                     var existeEstado = produto.tb_produto_icms_interestadual.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                     if (existeEstado != null)
                     {
                        nfeProduto.idCSTICMS = existeEstado.idCST;
                        nfeProduto.idOrigemMercadoria = existeEstado.idOrigem;
                        nfeProduto.MVA = existeEstado.MVA;
                        nfeProduto.PorcentagemReducao = existeEstado.PorcentagemReducao;

                        if (nfeProduto.idCSTICMS != 10)
                           nfeProduto.ValorAliquotaICMS = existeEstado.Aliquota;
                        else
                           nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                        {
                           nfeProduto.MVA = existeEstado.MVA;
                           nfeProduto.PorcentagemReducaoST = existeEstado.PorcentagemReducao;
                           nfeProduto.ValorAliquotaST = existeEstado.Aliquota;
                        }
                     }
                     else
                     {
                        nfeProduto.idCSTICMS = produto.tb_produto_imposto.idCSTICMS;
                        nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                           nfeProduto.MVA = produto.tb_produto_imposto.MVA;
                     }
                  }
                  else
                  {
                     nfeProduto.PorcentagemReducao = produto.tb_produto_imposto.PorcentagemReducao;
                     nfeProduto.idCSTICMS = produto.tb_produto_imposto.idCSTICMS;
                     nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS.ToInt32();
                     nfeProduto.ValorIPI = produto.tb_produto_imposto.AliquotaIPISaida.GetValueOrDefault();
                     if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                        nfeProduto.MVA = produto.tb_produto_imposto.MVA.ToInt32();
                  }
                  uscCSTICMS.Tag = "CST";
               }
            }
            nfeProduto.PorcentagemReducao = produtoDevolucao.PorcentagemReducao;
            nfeProduto.PorcentagemReducaoST = produtoDevolucao.PorcentagemReducao;
            //VALORES ICMS
            nfeProduto.ValorCreditoICMS = produtoDevolucao.ValorICMS;
            nfeProduto.ValorICMS = produtoDevolucao.ValorICMS;
            nfeProduto.ValorBaseCalculoICMS = produtoDevolucao.ValorBaseCalculoICMS;
            //VALORES ICMS ST
            if (produto.tb_produto_imposto.idCSTICMS.In(10, 70, 90) || produto.tb_produto_imposto.idCSOSN.In(201, 202, 203, 900))
            {
               if (produtoDevolucao.AliquotaICMS > 0)
               {
                  nfeProduto.ValorBaseCalculoICMSST = produtoDevolucao.ValorBaseCalculoICMSST;
                  nfeProduto.MVA = produtoDevolucao.MVA;
                  if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
                     nfeProduto.ValorAliquotaST = produtoDevolucao.AliquotaST;
                  else
                     nfeProduto.ValorAliquotaST = produtoDevolucao.AliquotaICMS;
                  nfeProduto.ValorICMSST = produtoDevolucao.ValorICMSST;
               }
            }
            //VALOR TOTAL PRDUTO
            nfeProduto.ValorTotalProduto = produtoDevolucao.ValorFinalProduto;
            //PIS
            nfeProduto.idCSTPIS = produto.tb_produto_imposto.idCSTPISCOFINS;
            nfeProduto.ValorBaseCalculoPIS = produtoDevolucao.ValorUnitario * produtoDevolucao.Quantidade;
            nfeProduto.ValorAliquotaPIS = produtoDevolucao.AliquotaPIS;
            nfeProduto.ValorPIS = produtoDevolucao.ValorPIS;
            //COFINS
            nfeProduto.idCSTCOFINS = produto.tb_produto_imposto.idCSTPISCOFINS;
            nfeProduto.ValorBaseCalculoCOFINS = produtoDevolucao.ValorUnitario * produtoDevolucao.Quantidade;
            nfeProduto.ValorAliquotaCOFINS = produtoDevolucao.AliquotaCOFINS;
            nfeProduto.ValorCOFINS = produtoDevolucao.ValorCOFINS;
            //IPI
            nfeProduto.idCSTIPI = produto.tb_produto_imposto.idCSTIPISaida;
            nfeProduto.ValorBaseCalculoIPI = produtoDevolucao.ValorUnitario * produtoDevolucao.Quantidade;
            nfeProduto.ValorAliquotaIPI = produtoDevolucao.AliquotaIPISaida;
            nfeProduto.ValorIPI = produtoDevolucao.ValorIPI;

            nfeProdutoObservable.Add(nfeProduto);

            //partilhaICMS
            decimal baseCalculoPartilha = 0;
            baseCalculoPartilha = (produtoDevolucao.ValorUnitario * produtoDevolucao.Quantidade);
            CalcularPartilhaICMS(baseCalculoPartilha, produto, nfeProduto);
            //

            //Contar Produtos
            int contador = 1;
            int? numeroItem = 0;

            foreach (tb_nfe_produto itemProduto in nfeProdutoObservable)
            {
               itemProduto.NumeroItem = contador;
               contador++;

               if (nfeProduto.NumeroItem == 1)
                  numeroItem = item.NumeroItem;
            }
            //setar a natureza de operação
            if (string.IsNullOrEmpty(txtNaturezaOperacao.Text) || numeroItem == 1)
            {
               var cfop = new CFOPBusiness().BuscarPorId(nfeProdutoObservable.FirstOrDefault().idCFOP.Value);
               txtNaturezaOperacao.Text = cfop.Descricao;
            }
            dgProdutos.ItemsSource = nfeProdutoObservable;

            valorTotal += produtoDevolucao.ValorFinalProduto;
         }
         txtValorTotalNota.Text = valorTotal.ToString("N2");
      }
      private void PreencherDadosProdutoDevolucaoVenda(object DadosProduto)
      {
         decimal valorTotal = 0;
         if (DadosProduto is List<tb_devolucao_venda_produto>)
            DadosProduto = DadosProduto as List<tb_devolucao_venda_produto>;

         foreach (var item in (dynamic)DadosProduto)
         {
            var produtoDevolucao = item as tb_devolucao_venda_produto;
            var nfeProduto = new tb_nfe_produto();
            if (nfe != null && nfe.id > 0)
               nfeProduto.idNFe = nfe.id;
            nfeProduto.idProduto = produtoDevolucao.idProduto;

            nfeProduto.Quantidade = produtoDevolucao.Quantidade.ToInt32();
            nfeProduto.ValorUnitario = produtoDevolucao.ValorUnitario;

            nfeProduto.tb_produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);

            var produto = nfeProduto.tb_produto;
            nfeProduto.tb_produto.idUnidade = produtoDevolucao.tb_produto.idUnidade;
            if (produtoDevolucao.tb_produto.idUnidade > 0)
               nfeProduto.tb_produto.tb_unidade = new UnidadeBusiness().BuscarPorId(produtoDevolucao.tb_produto.idUnidade);

            var cliente = clienteBusiness.BuscarPorId(uscClienteDestinatario.Id.Value);
            int idCliente = 0;
            idCliente = produtoDevolucao.tb_devolucao_venda.idCliente;

            var estadoCliente = clienteBusiness.BuscarPorId(idCliente).tb_cidade.tb_estado;
            if ((empresa.tb_cidade.idEstado != estadoCliente.id
              && empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id) && produto.UtilizarTabelaICMS && produto.idTabelaICMS.HasValue)
            {
               this.aliquotaIntraestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoOrigem == cliente.tb_cidade.idEstado && x.idEstadoDestino == cliente.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
               this.aliquotaInterestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoDestino == cliente.tb_cidade.idEstado && x.idEstadoOrigem == empresa.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
            }
            else if (empresa.tb_cidade.idEstado != estadoCliente.id)
            {
               var lista = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(nfeProduto.idProduto);
               if (lista != null && lista.Count > 0)
               {
                  var existeEstado = lista.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                  if (existeEstado != null)
                  {
                     this.aliquotaInterestadual = existeEstado.Aliquota;
                  }
               }
            }

            nfeProduto.idCFOP = produtoDevolucao.idCFOP;
            nfeProduto.idOrigemMercadoria = produto.tb_produto_imposto.idOrigemMercadoria;

            if (empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id)
            {
               if (new ParametroBusiness().BuscarParametroVigente().idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
               {
                  nfeProduto.idCSOSN = produto.tb_produto_imposto.idCSOSN;
                  uscCSTICMS.Id = nfeProduto.idCSOSN;
                  nfeProduto.ValorAliquotaICMS = produtoDevolucao.AliquotaICMS;
                  uscCSTICMS.Tag = "CSOSN";
               }
               else
               {
                  produto.tb_produto_icms_interestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(produto.id);
                  if (empresa.tb_cidade.idEstado != estadoCliente.id && produto.tb_produto_icms_interestadual != null && produto.tb_produto_icms_interestadual.Any() && !produto.UtilizarTabelaICMS)
                  {
                     var existeEstado = produto.tb_produto_icms_interestadual.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                     if (existeEstado != null)
                     {
                        nfeProduto.idCSTICMS = existeEstado.idCST;
                        nfeProduto.idOrigemMercadoria = existeEstado.idOrigem;
                        nfeProduto.MVA = existeEstado.MVA;
                        nfeProduto.PorcentagemReducao = existeEstado.PorcentagemReducao;

                        if (nfeProduto.idCSTICMS != 10)
                           nfeProduto.ValorAliquotaICMS = existeEstado.Aliquota;
                        else
                           nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                        {
                           nfeProduto.MVA = existeEstado.MVA;
                           nfeProduto.PorcentagemReducaoST = existeEstado.PorcentagemReducao;
                           nfeProduto.ValorAliquotaST = existeEstado.Aliquota;
                        }
                     }
                     else
                     {
                        nfeProduto.idCSTICMS = produto.tb_produto_imposto.idCSTICMS;
                        nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                           nfeProduto.MVA = produto.tb_produto_imposto.MVA;
                     }
                  }
                  else
                  {
                     nfeProduto.PorcentagemReducao = produto.tb_produto_imposto.PorcentagemReducao;
                     nfeProduto.idCSTICMS = produto.tb_produto_imposto.idCSTICMS;
                     nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS.ToInt32();
                     nfeProduto.ValorIPI = produto.tb_produto_imposto.AliquotaIPISaida.GetValueOrDefault();
                     if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                        nfeProduto.MVA = produto.tb_produto_imposto.MVA.ToInt32();
                  }
                  uscCSTICMS.Tag = "CST";
               }
            }
            nfeProduto.PorcentagemReducao = produtoDevolucao.PorcentagemReducao;
            nfeProduto.PorcentagemReducaoST = produtoDevolucao.PorcentagemReducao;
            //VALORES ICMS
            nfeProduto.ValorCreditoICMS = produtoDevolucao.ValorICMS;
            nfeProduto.ValorICMS = produtoDevolucao.ValorICMS;
            nfeProduto.ValorBaseCalculoICMS = produtoDevolucao.ValorBaseCalculoICMS;
            //VALORES ICMS ST
            if (produto.tb_produto_imposto.idCSTICMS.In(10, 70, 90) || produto.tb_produto_imposto.idCSOSN.In(201, 202, 203, 900))
            {
               if (produtoDevolucao.AliquotaICMS > 0)
               {
                  nfeProduto.ValorBaseCalculoICMSST = produtoDevolucao.ValorBaseCalculoICMSST;
                  nfeProduto.MVA = produtoDevolucao.MVA;
                  if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
                     nfeProduto.ValorAliquotaST = produtoDevolucao.AliquotaST;
                  else
                     nfeProduto.ValorAliquotaST = produtoDevolucao.AliquotaICMS;
                  nfeProduto.ValorICMSST = produtoDevolucao.ValorICMSST;
               }
            }
            //VALOR TOTAL PRDUTO
            nfeProduto.ValorTotalProduto = produtoDevolucao.ValorFinalProduto;
            //PIS
            nfeProduto.idCSTPIS = produto.tb_produto_imposto.idCSTPISCOFINS;
            nfeProduto.ValorBaseCalculoPIS = produtoDevolucao.ValorUnitario * produtoDevolucao.Quantidade;
            nfeProduto.ValorAliquotaPIS = produtoDevolucao.AliquotaPIS;
            nfeProduto.ValorPIS = produtoDevolucao.ValorPIS;
            //COFINS
            nfeProduto.idCSTCOFINS = produto.tb_produto_imposto.idCSTPISCOFINS;
            nfeProduto.ValorBaseCalculoCOFINS = produtoDevolucao.ValorUnitario * produtoDevolucao.Quantidade;
            nfeProduto.ValorAliquotaCOFINS = produtoDevolucao.AliquotaCOFINS;
            nfeProduto.ValorCOFINS = produtoDevolucao.ValorCOFINS;
            //IPI
            nfeProduto.idCSTIPI = produto.tb_produto_imposto.idCSTIPISaida;
            nfeProduto.ValorBaseCalculoIPI = produtoDevolucao.ValorUnitario * produtoDevolucao.Quantidade;
            nfeProduto.ValorAliquotaIPI = produtoDevolucao.AliquotaIPISaida;
            nfeProduto.ValorIPI = produtoDevolucao.ValorIPI;

            nfeProdutoObservable.Add(nfeProduto);
            //partilhaICMS
            decimal baseCalculoPartilha = 0;
            baseCalculoPartilha = (produtoDevolucao.ValorUnitario * produtoDevolucao.Quantidade);
            CalcularPartilhaICMS(baseCalculoPartilha, produto, nfeProduto);
            //

            //Contar Produtos
            int contador = 1;
            int? numeroItem = 0;

            foreach (tb_nfe_produto itemProduto in nfeProdutoObservable)
            {
               itemProduto.NumeroItem = contador;
               contador++;

               if (nfeProduto.NumeroItem == 1)
                  numeroItem = item.NumeroItem;
            }
            //setar a natureza de operação
            if (string.IsNullOrEmpty(txtNaturezaOperacao.Text) || numeroItem == 1)
            {
               var cfop = new CFOPBusiness().BuscarPorId(nfeProdutoObservable.FirstOrDefault().idCFOP.Value);
               txtNaturezaOperacao.Text = cfop.Descricao;
            }
            dgProdutos.ItemsSource = nfeProdutoObservable;

            valorTotal += produtoDevolucao.ValorFinalProduto;
         }
         txtValorTotalNota.Text = valorTotal.ToString("N2");
      }
      private void PreencherDadosProdutoOrigemTelaConsulta(tb_nfe_referencia_cupom nfeRefCupom = null, tb_devolucao_venda_nfe nfeDevolucaoVenda = null, tb_devolucao_compra_nfe nfeDevolucaoCompra = null, tb_nfe nfe = null)
      {
         decimal valorTotal = 0;
         int idNfe = 0;
         if (nfeRefCupom != null)
            idNfe = nfeRefCupom.idNFe;
         else if (nfeDevolucaoVenda != null)
            idNfe = nfeDevolucaoVenda.idNFe;
         else if (nfeDevolucaoCompra != null)
            idNfe = nfeDevolucaoCompra.idNFe;
         else if (nfe != null)
            idNfe = nfe.id;

         var DadosProduto = new NFeProdutoBusiness().BuscarPorIdNota(idNfe);

         foreach (var item in DadosProduto)
         {
            var produtoNfe = item;
            var nfeProduto = new tb_nfe_produto();
            if (nfe != null && nfe.id > 0)
               nfeProduto.idNFe = nfe.id;
            else
               nfeProduto.idNFe = idNfe;

            nfeProduto.id = produtoNfe.id;
            nfeProduto.idProduto = produtoNfe.idProduto;

            nfeProduto.Quantidade = produtoNfe.Quantidade.ToInt32();
            nfeProduto.ValorUnitario = produtoNfe.ValorUnitario;
            nfeProduto.ValorDesconto = produtoNfe.ValorDesconto;
            nfeProduto.ValorFrete = produtoNfe.ValorFrete;
            nfeProduto.ValorOutrasDespesas = produtoNfe.ValorOutrasDespesas;
            nfeProduto.ValorSeguro = produtoNfe.ValorSeguro;

            nfeProduto.tb_produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);

            var produto = nfeProduto.tb_produto;

            int idCliente = 0;
            var dadosNfe = new NFeBusiness().BuscarPorId(produtoNfe.idNFe);
            idCliente = dadosNfe.idCliente;
            uscClienteDestinatario.Id = dadosNfe.idCliente;
            var estadoCliente = clienteBusiness.BuscarPorId(idCliente).tb_cidade.tb_estado;
            if ((empresa.tb_cidade.idEstado != estadoCliente.id
              && empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id) && produto.UtilizarTabelaICMS && produto.idTabelaICMS.HasValue)
            {
               this.aliquotaIntraestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoOrigem == cliente.tb_cidade.idEstado && x.idEstadoDestino == cliente.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
               this.aliquotaInterestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoDestino == cliente.tb_cidade.idEstado && x.idEstadoOrigem == empresa.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
            }
            else if (empresa.tb_cidade.idEstado != estadoCliente.id)
            {
               var lista = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(nfeProduto.idProduto);
               if (lista != null && lista.Count > 0)
               {
                  var existeEstado = lista.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                  if (existeEstado != null)
                  {
                     this.aliquotaInterestadual = existeEstado.Aliquota;
                  }
               }
            }

            nfeProduto.idCFOP = produtoNfe.idCFOP;
            nfeProduto.idOrigemMercadoria = produto.tb_produto_imposto.idOrigemMercadoria;

            if (empresa.tb_cidade.tb_estado.idPais == estadoCliente.tb_pais.id && nfe == null)
            {
               if (new ParametroBusiness().BuscarParametroVigente().idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
               {
                  nfeProduto.idCSOSN = produto.tb_produto_imposto.idCSOSN;
                  uscCSTICMS.Id = nfeProduto.idCSOSN;
                  nfeProduto.ValorAliquotaICMS = produtoNfe.ValorAliquotaICMS;
                  uscCSTICMS.Tag = "CSOSN";
               }
               else
               {
                  produto.tb_produto_icms_interestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(produto.id);
                  if (empresa.tb_cidade.idEstado != estadoCliente.id && produto.tb_produto_icms_interestadual != null && produto.tb_produto_icms_interestadual.Any() && !produto.UtilizarTabelaICMS)
                  {
                     var existeEstado = produto.tb_produto_icms_interestadual.Where(x => x.idEstado == estadoCliente.id).FirstOrDefault();
                     if (existeEstado != null)
                     {
                        nfeProduto.idCSTICMS = existeEstado.idCST;
                        nfeProduto.idOrigemMercadoria = existeEstado.idOrigem;
                        nfeProduto.MVA = existeEstado.MVA;
                        nfeProduto.PorcentagemReducao = existeEstado.PorcentagemReducao;

                        if (nfeProduto.idCSTICMS != 10)
                           nfeProduto.ValorAliquotaICMS = existeEstado.Aliquota;
                        else
                           nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                        {
                           nfeProduto.MVA = existeEstado.MVA;
                           nfeProduto.PorcentagemReducaoST = existeEstado.PorcentagemReducao;
                           nfeProduto.ValorAliquotaST = existeEstado.Aliquota;
                        }
                     }
                     else
                     {
                        nfeProduto.idCSTICMS = produto.tb_produto_imposto.idCSTICMS;
                        nfeProduto.ValorAliquotaICMS = produto.tb_produto_imposto.AliquotaICMS;

                        if (nfeProduto.idCSTICMS == 10 || nfeProduto.idCSTICMS == 70 || nfeProduto.idCSOSN == 201 || nfeProduto.idCSOSN == 202 || nfeProduto.idCSOSN == 203)
                           nfeProduto.MVA = produto.tb_produto_imposto.MVA;
                     }
                  }
                  uscCSTICMS.Tag = "CST";
               }
            }
            nfeProduto.PorcentagemReducao = produtoNfe.PorcentagemReducao;
            nfeProduto.PorcentagemReducaoST = produtoNfe.PorcentagemReducao;
            //VALORES ICMS
            if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
               nfeProduto.idCSOSN = produtoNfe.idCSOSN;
            else
               nfeProduto.idCSTICMS = produtoNfe.idCSTICMS;
            nfeProduto.ValorCreditoICMS = produtoNfe.ValorICMS;
            nfeProduto.ValorICMS = produtoNfe.ValorICMS;
            nfeProduto.ValorBaseCalculoICMS = produtoNfe.ValorBaseCalculoICMS;
            nfeProduto.ValorAliquotaICMS = produtoNfe.ValorAliquotaICMS;

            if (produtoNfe.idCSTICMS.In(51))
               nfeProduto.PorcentagemDiferimento = produtoNfe.PorcentagemDiferimento;

            if (produtoNfe.idCSTICMS.In(60, 500))
            {
               nfeProduto.ValorBaseCalculoICMSSTRet = produtoNfe.ValorBaseCalculoICMSSTRet;
               nfeProduto.ValorAliquotaSTRet = produtoNfe.ValorAliquotaSTRet;
               nfeProduto.ValorICMSSubstituto = produtoNfe.ValorICMSSubstituto;
               nfeProduto.ValorICMSSTRetido = produtoNfe.ValorICMSSTRetido;
               nfeProduto.PorcentagemFCPRet = produtoNfe.PorcentagemFCPRet;
            }

            //VALORES ICMS ST
            if (produtoNfe.idCSTICMS.In(10, 70, 90) || produtoNfe.idCSOSN.In(201, 202, 203, 900))
            {
               if (produtoNfe.ValorAliquotaICMS > 0)
               {
                  nfeProduto.ValorBaseCalculoICMSST = produtoNfe.ValorBaseCalculoICMSST;
                  nfeProduto.MVA = produtoNfe.MVA;
                  if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
                     nfeProduto.ValorAliquotaST = produtoNfe.ValorAliquotaST;
                  else
                     nfeProduto.ValorAliquotaST = produtoNfe.ValorAliquotaICMS;
                  nfeProduto.ValorICMSST = produtoNfe.ValorICMSST;
               }
            }
            //VALOR TOTAL PRDUTO
            decimal valorTotalProduto = produtoNfe.ValorTotalProduto.GetValueOrDefault();
            nfeProduto.ValorTotalProduto = valorTotalProduto;
            //PIS
            nfeProduto.idCSTPIS = produtoNfe.idCSTPIS;
            nfeProduto.ValorBaseCalculoPIS = produtoNfe.ValorUnitario * produtoNfe.Quantidade;
            nfeProduto.ValorAliquotaPIS = produtoNfe.ValorAliquotaPIS;
            nfeProduto.ValorPIS = produtoNfe.ValorPIS;
            //COFINS
            nfeProduto.idCSTCOFINS = produtoNfe.idCSTCOFINS;
            nfeProduto.ValorBaseCalculoCOFINS = produtoNfe.ValorUnitario * produtoNfe.Quantidade;
            nfeProduto.ValorAliquotaCOFINS = produtoNfe.ValorAliquotaCOFINS;
            nfeProduto.ValorCOFINS = produtoNfe.ValorCOFINS;
            //IPI
            if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional && parametro.Industrial)
            {
               nfeProduto.idCSTIPI = produtoNfe.idCSTIPI;
               nfeProduto.ValorBaseCalculoIPI = produtoNfe.ValorUnitario * produtoNfe.Quantidade;
               nfeProduto.ValorAliquotaIPI = produtoNfe.ValorAliquotaIPI;
               nfeProduto.ValorIPI = produtoNfe.ValorIPI;
            }
            // preencher imposto de importação
            if (NotaImportacao())
            {
               var declaracao = new NFeDeclaracaoImportacaoBusiness().BuscarPorIdNFeProdutoUnique(produtoNfe.id);
               nfeProduto.tb_nfe_declaracao_importacao.Add(declaracao);
               var declaracaoAdicao = new NFeDeclaracaoImportacaoAdicaoBusiness().BuscarPorIdNFeDeclaracaoImportacao(declaracao.id);
               declaracao.tb_nfe_declaracao_importacao_adicao = declaracaoAdicao;
            }

            //imposto importação nfe
            nfeProduto.ValorBCII = produtoNfe.ValorBCII;
            nfeProduto.ValorII = produtoNfe.ValorII;
            nfeProduto.ValorDespesasAduaneiras = produtoNfe.ValorDespesasAduaneiras;
            nfeProduto.ValorIOF = produtoNfe.ValorIOF;

            //partilhaICMS
            decimal baseCalculoPartilha = 0;
            baseCalculoPartilha = (produtoNfe.ValorUnitario * produtoNfe.Quantidade);
            nfeProduto.idPartilhaICMS = produtoNfe.idPartilhaICMS;
            nfeProduto.ValorBaseCalculoPartilha = produtoNfe.ValorBaseCalculoPartilha;
            nfeProduto.ValorInterestadual = produtoNfe.ValorInterestadual;
            nfeProduto.PorcentagemOrigemPartilha = produtoNfe.PorcentagemOrigemPartilha;
            nfeProduto.ValorIntraestadual = produtoNfe.ValorIntraestadual;
            nfeProduto.PorcentagemDestinoPartilha = produtoNfe.PorcentagemDestinoPartilha;
            nfeProduto.PorcentagemFCP = produtoNfe.PorcentagemFCP;
            nfeProduto.ValorFCP = produtoNfe.ValorFCP;
            nfeProduto.PorcentagemIntraestadual = produtoNfe.PorcentagemIntraestadual;
            nfeProduto.PorcentagemInterestadual = produtoNfe.PorcentagemInterestadual;

            nfeProdutoObservable.Add(nfeProduto);

            //Contar Produtos
            int contador = 1;
            int? numeroItem = 0;

            foreach (tb_nfe_produto itemProduto in nfeProdutoObservable)
            {
               itemProduto.NumeroItem = contador;
               contador++;

               if (nfeProduto.NumeroItem == 1)
                  numeroItem = item.NumeroItem;
            }
            //setar a natureza de operação
            if (string.IsNullOrEmpty(txtNaturezaOperacao.Text) || numeroItem == 1)
            {
               var cfop = new CFOPBusiness().BuscarPorId(nfeProdutoObservable.FirstOrDefault().idCFOP.Value);
               txtNaturezaOperacao.Text = cfop.Descricao;
            }
            dgProdutos.ItemsSource = nfeProdutoObservable;

            valorTotal += valorTotalProduto;

         }
         txtValorTotalNota.Text = valorTotal.ToString("N2");

      }
      #endregion

      #region  Preencher aba Informação Complementar
      private void PreencherInformacaoComplementar(tb_nfe_referencia_cupom refCupom = null, tb_devolucao_venda_nfe nfeDevolucaoVenda = null, tb_devolucao_compra_nfe nfeDevolucaoCompra = null, tb_nfe nfeOrigim = null)
      {
         int idNFe = 0;
         if (refCupom != null)
            idNFe = refCupom.idNFe;
         else if (nfeDevolucaoVenda != null)
            idNFe = nfeDevolucaoVenda.idNFe;
         else if (nfeDevolucaoCompra != null)
            idNFe = nfeDevolucaoCompra.idNFe;
         else if (nfeOrigim != null)
            idNFe = nfeOrigim.id;

         var nfe = new NFeBusiness().BuscarPorId(idNFe);
         if (nfe != null)
         {
            txtInfEditaveis.Text = nfe.InformacoesEditaveis;
            txtInfComplementaresPadrao.Text = nfe.InformacoesComplementares;
            txtImpostoAproximado.Text = nfe.InformacoesFisco;
         }
      }
      #endregion

      private void ConfiguracoesIniciais()
      {
         //CarregarTpIntermedio();
         //CarregarTpViaTransf();
         txtOrigemNfe.Text = "Emissão Nfe";
         nfe = new tb_nfe();
         empresaBusiness = new EmpresaBusiness();
         empresa = empresaBusiness.BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);
         tbiReferenciaDevolucao.Visibility = Visibility.Collapsed;
         tbiErros.Visibility = Visibility.Collapsed;
         tbiCartaCorrecao.Visibility = Visibility.Collapsed;
         tbiProduto.IsEnabled = false;
         DesabilitarCamposDestinatario();
         parametro = new ParametroBusiness().BuscarParametroVigente();
         if (parametro != null)
         {
            decimal NumeroNotaParametro = parametro.tb_nf_configuracao.UltimoNumeroNota;
            decimal NumeroLote = parametro.tb_nf_configuracao.UltimoIdLote;

            txtNumeroNota.Text = (NumeroNotaParametro + 1).ToString();
            NumeroLoteCont = (NumeroLote + 1).ToInt32();

            txtNumeroSerie.Text = parametro.tb_nf_configuracao.NumeroSerie.ToString();
            if (parametro.tb_nf_configuracao.NFeProducao)
               cboAmbiente.SelectedValue = TipoAmbiente.Producao;
            else
               cboAmbiente.SelectedValue = TipoAmbiente.Homologacao;
         }
         cboTipoEmissao.SelectedValue = TipoEmissao.teNormal;
         cboTipoNota.SelectedValue = (int)EnumTipoNota.Saida;
         TipoNota = (int)EnumTipoNota.Saida;

         txtDataEmissao.Text = DateTime.Now.ToShortDateString();
         txtDataSaida.Text = DateTime.Now.ToShortDateString();
         txtDataEmissao.IsEnabled = false;
         txtDataReferencia.Text = DateTime.Now.ToShortDateString();

         btnVisualizarDanfe.IsEnabled = false;
         btnNovaNfe.IsEnabled = false;
         parametro = new ParametroBusiness().BuscarParametroVigente();
         empresaBusiness = new EmpresaBusiness();
         nfeBusiness = new NFeBusiness();

         new ClienteConfiguracaoHelper().Configurar(uscClienteDestinatario);
         new TransportadoraConfiguracaoHelper().Configurar(uscTransportadora);
         new ProdutoConfiguracaoHelper().ConfigurarSomenteAtivo(uscProduto);
         new CSTPISCOFINSConfiguracaoHelper().Configurar(uscCSTPIS, true);
         new CSTPISCOFINSConfiguracaoHelper().Configurar(uscCSTCOFINS, true);
         new CSTIPIConfiguracaoHelper().Configurar(uscCSTIPI, true);
         new OrigemMercadoriaConfiguracaoHelper().Configurar(uscOrigem);

         CarregarComboFinalidade();
         cboFinalidade.SelectedValue = (int)FinalidadeNFe.fnNormal;
         CarregarComboTipoEmissao();
         CarregarComboFormaPagamento();
         CarregarComboTipoNota();
         CarregarComboFrete();
         CarregarComboAmbiente();
         PreencherEmitente();
         CarregarComboContribuinteICMS();
         CarregarComboAtacadoVarejo();
         CarregarComboModeloCupom();
         CarregarComboEspecieVolume();
         btnAdicionarProduto.Tag = "Adicionar";
         nfeProdutoObservable = new ObservableCollection<tb_nfe_produto>();
         formaPagamentoObservable = new ObservableCollection<tb_nfe_formapagamento>();
         pagamentoObservable = new ObservableCollection<tb_nfe_pagamento>();
         nfeReferenciaContranotaObservable = new ObservableCollection<tb_nfe_referencia_contranota>();
         btnExcluirProduto.Visibility = Visibility.Collapsed;
         cboFormaPagamento.SelectedValue = IndicadorPagamento.ipVista;
         btnTransmitir.IsEnabled = false;
         clienteBusiness = new ClienteBusiness();
         uscTransportadora.IsEnabled = false;
         HabilitarCamposTransportadora();
         tbiPagamento.IsEnabled = false;
         tbiTotais.IsEnabled = false;
         CarregarComboInformacoesComplementares();
         if (nfe.id > 0)
         {
            cboInformacoesComplementares.SelectedValue = null;
            txtInfComplementaresPadrao.Text = nfe.InformacoesComplementares;
         }
         else if (parametro != null && parametro.tb_parametro_lancamento.idInformacoesComplementares.HasValue)
            cboInformacoesComplementares.SelectedValue = parametro.tb_parametro_lancamento.idInformacoesComplementares.Value;
         rdbDevolucaoNota.IsChecked = true;
         nfeReferenciaObservable = new ObservableCollection<tb_nfe_referencia>();
         nfeReferenciaCupomObservable = new ObservableCollection<tb_nfe_referencia_cupom>();
         produtoBusiness = new ProdutoBusiness();
         //declaração de importação
         Utilidades.CarregaCombo<EstadoBusiness, tb_estado>(cboUFAdquirente, "id", "Sigla");
         Utilidades.CarregaCombo<EstadoBusiness, tb_estado>(cboUFDesembarque, "id", "Sigla");
         CarregarTpIntermedio();
         CarregarTpViaTransf();
         txtDataDelacarao.Text = DateTime.Now.ToShortDateString();
         txtDataDesembarque.Text = DateTime.Now.ToShortDateString();
         adicaoObservable = new ObservableCollection<tb_nfe_declaracao_importacao_adicao>();
      }
      #region Aba Cabeçalho
      private void PreencherCampos()
      {
         txtStatus.Text = ((EnumStatusNFe)nfe.idNFStatus).ToString();//new NFStatusBusiness().BuscarPorId( nfe.idNFStatus.GetValueOrDefault());
         if (EnumStatusNFe.EmitidaComSucesso == (EnumStatusNFe)nfe.idNFStatus)
         {
            btnSalvar.IsEnabled = false;
            btnTransmitir.IsEnabled = false;
            btnNovaNfe.IsEnabled = true;
         }

         txtStatus.Foreground = AlterarCorStatus(txtStatus.Text);

         this.nfe = nfeBusiness.BuscarPorId(nfe.id);
         if (this.nfe.idNfeOrigem == 0)
            txtOrigemNfe.Text = "Ordem de Serviço";
         else if (this.nfe.idNfeOrigem == 1)
            txtOrigemNfe.Text = "Venda";
         else if (this.nfe.idNfeOrigem == 2)
            txtOrigemNfe.Text = "Venda Rápida";
         else if (this.nfe.idNfeOrigem == 3)
            txtOrigemNfe.Text = "Compra";
         else if (this.nfe.idNfeOrigem == 4)
            txtOrigemNfe.Text = "Compra Rápida";
         else if (this.nfe.idNfeOrigem == 5)
         {
            txtOrigemNfe.Text = "Emissão Nfe";
         }


         txtChave.Text = nfe.Chave;

         cboTipoEmissao.SelectedValue = (TipoEmissao)nfe.TipoEmissao;
      }
      private void CboFinalidade_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         HabilitarReferencia();
      }

      private void HabilitarReferencia()
      {
         tbiReferenciaDevolucao.Visibility = Visibility.Collapsed;

         if (cboFinalidade.SelectedItem != null)
         {
            var itemSelecionado = cboFinalidade.SelectedValue.ToInt32();
            if (itemSelecionado == (int)FinalidadeNFe.fnComplementar)
            {
               tbiReferenciaDevolucao.Visibility = Visibility.Visible;

            }
            else if (itemSelecionado == (int)FinalidadeNFe.fnAjuste)
            {
               tbiReferenciaDevolucao.Visibility = Visibility.Visible;
            }
            else if (itemSelecionado == (int)FinalidadeNFe.fnDevolucao || cboTipoNota.SelectedIndex == (int)EnumTipoNota.DevolucaoVenda)
            {
               tbiReferenciaDevolucao.Visibility = Visibility.Visible;
            }
            else if (cboTipoNota.SelectedIndex == (int)EnumTipoNota.DevolucaoCompra)
            {
               tbiReferenciaDevolucao.Visibility = Visibility.Visible;
               rdbDevolucaoCupom.IsEnabled = false;
            }
            else if (cboTipoNota.SelectedIndex == (int)EnumTipoNota.Entrada && cliente != null && cliente.ProdutorRural == true)
            {
               tbiReferenciaDevolucao.Visibility = Visibility.Visible;
               tbiReferenciaContraNota.Visibility = Visibility.Visible;
               txtNumeroNotaReferencia.IsEnabled = true;
               txtNumeroSerieReferencia.IsEnabled = true;
               btnAdicionarReferenciaContranota.IsEnabled = true;
               rdbDevolucaoNota.IsEnabled = false;
               rdbDevolucaoCupom.IsEnabled = false;
               tbiNfe.IsEnabled = false;
               tbiCupom.IsEnabled = false;
               txtchaveReferencia.IsEnabled = false;
               btnAdicionarChave.IsEnabled = false;
            }
         }
      }
      private void CarregarComboTipoEmissao()
      {
         cboTipoEmissao.ItemsSource = EnumHelper.ToList(typeof(TipoEmissao));
         cboTipoEmissao.DisplayMemberPath = "Value";
         cboTipoEmissao.SelectedValuePath = "Key";
         // cboFinalidade.SelectedIndex = 0;
      }
      private void CarregarComboFinalidade()
      {
         Dictionary<int, string> listaOpcao = new Dictionary<int, string>();
         listaOpcao.Add((int)FinalidadeNFe.fnNormal, "Normal");
         listaOpcao.Add((int)FinalidadeNFe.fnAjuste, "Ajuste");
         listaOpcao.Add((int)FinalidadeNFe.fnComplementar, "Complemento");
         listaOpcao.Add((int)FinalidadeNFe.fnDevolucao, "Devolução");
         Utilidades.CarregaCombo<int, string>(cboFinalidade, listaOpcao);
      }
      private void CarregarComboTipoNota()
      {
         /*** Cria uma lista informando o da Chave e do Valor do Combobox  **/
         Dictionary<int, string> listaOpcao = new Dictionary<int, string>();
         listaOpcao.Add((int)EnumTipoNota.Entrada, "Entrada");
         listaOpcao.Add((int)EnumTipoNota.Saida, "Saída");
         listaOpcao.Add((int)EnumTipoNota.DevolucaoCompra, "Devolução Compra");
         listaOpcao.Add((int)EnumTipoNota.DevolucaoVenda, "Devolução Venda");
         Utilidades.CarregaCombo<int, string>(cboTipoNota, listaOpcao);
      }
      private void CarregarComboModeloCupom()
      {
         Dictionary<string, string> listaOpcao = new Dictionary<string, string>();
         listaOpcao.Add("2B", "2B");
         listaOpcao.Add("2C", "2C");
         listaOpcao.Add("2D", "2D");
         cboModeloEcf.ItemsSource = listaOpcao;
         cboModeloEcf.DisplayMemberPath = "Value";
         cboModeloEcf.SelectedValuePath = "Value";
         cboModeloEcf.SelectedValue = "2B";
      }
      private void CarregarComboAtacadoVarejo()
      {
         cboAtacadoVarejo.ItemsSource = EnumHelper.ToList(typeof(EnumAtacadoVarejo));
         cboAtacadoVarejo.DisplayMemberPath = "Value";
         cboAtacadoVarejo.SelectedValuePath = "Key";
         cboAtacadoVarejo.SelectedIndex = 1;

         if (PCInfo.Base.Core.Principal.Licenca.ModuloSistema != EnumProduto.NFe)
         {
            if (parametro != null && parametro.tb_parametro_lancamento != null)
               cboAtacadoVarejo.SelectedValue = parametro.tb_parametro_lancamento.Varejo;
         }
      }
      private void CarregarComboAmbiente()
      {
         cboAmbiente.ItemsSource = EnumHelper.ToList(typeof(TipoAmbiente));
         cboAmbiente.DisplayMemberPath = "Value";
         cboAmbiente.SelectedValuePath = "Key";

      }
      private void CarregarComboContribuinteICMS()
      {
         cboContribuinteIcmsDestinatario.ItemsSource = EnumHelper.ToList(typeof(indIEDest));
         cboContribuinteIcmsDestinatario.DisplayMemberPath = "Value";
         cboContribuinteIcmsDestinatario.SelectedValuePath = "Key";
      }
      private void CarregarComboFormaPagamento()
      {
         cboFormaPagamento.ItemsSource = EnumHelper.ToList(typeof(IndicadorPagamento));
         cboFormaPagamento.DisplayMemberPath = "Value";
         cboFormaPagamento.SelectedValuePath = "Key";
      }
      private void CarregarComboFrete()
      {
         cboTipoFrete.ItemsSource = EnumHelper.ToList(typeof(ModalidadeFrete));
         cboTipoFrete.DisplayMemberPath = "Value";
         cboTipoFrete.SelectedValuePath = "Key";
         cboTipoFrete.SelectedValue = ModalidadeFrete.SemFrete;
      }
      private void CarregarComboInformacoesComplementares()
      {
         InformacoesComplementaresNFeBusiness business = new InformacoesComplementaresNFeBusiness();
         List<tb_informacoes_complementares_nfe> listaInformacoes = new List<tb_informacoes_complementares_nfe>();
         listaInformacoes = business.BuscarPorCondicao(x => x.idEmpresa == null || x.idEmpresa == PCInfo.Base.Core.Principal.Empresa.id).ToList();
         listaInformacoes.Insert(0, new tb_informacoes_complementares_nfe() { id = -1, Nome = "", Descricao = "" });
         cboInformacoesComplementares.DisplayMemberPath = "Nome";
         cboInformacoesComplementares.SelectedValuePath = "id";
         cboInformacoesComplementares.ItemsSource = listaInformacoes;
      }
      private Brush AlterarCorStatus(string texto)
      {
         Brush cor = Brushes.Black;
         if (texto != null)
         {
            string statusNFe = texto.ToString();
            if (statusNFe.In("Emitida com sucesso", "EmitidaComSucesso"))
            {
               cor = Brushes.Green;
            }
            else if (statusNFe.In("Aguardando Envio", "AguardandoEnvio"))
            {
               cor = Brushes.Blue;
            }
            else if (statusNFe.Equals("Erro na Emissão de NFe") || statusNFe.Equals("ErroEmissaoNFe") || statusNFe.Equals("Erro no Cancelamento") || statusNFe.Equals("Inutilizada") || statusNFe.Equals("Denegada"))
            {
               cor = Brushes.Red;
            }
            else if (statusNFe.In("Aguardando Processamento", "AguardandoProcessamento", "Aguardando Cancelamento", "AguardandoCancelamento"))
            {
               cor = Brushes.RoyalBlue;
            }
            else if (statusNFe.Equals("Cancelada"))
            {
               cor = Brushes.Orange;
            }
            else if (statusNFe.In("EpecAguardandoEnvio", "Epec Aguardando Envio"))
            {
               cor = Brushes.Purple;
            }
            else
            {
               cor = Brushes.Black;
            }
         }

         return cor;
      }
      private void PreencherEmitente()
      {
         var empresa = empresaBusiness.BuscarPorId(PCInfo.Base.Core.Principal.Empresa.id);
         txtRazaoSocial.Text = empresa.RazaoSocial;
         txtInscricaoEstadual.Text = empresa.InscricaoEstadual;
         txtCpfCnpjEmitente.Text = empresa.NumeroInscricao; //Configurar máscara do textbox antes de preencher - pegar da tela de empresa
         DefinirMascaraTipoInscricao(empresa.TipoInscricao, txtCpfCnpjEmitente);
         txtEndereco.Text = empresa.EnderecoTipoRua;
         txtCep.Text = empresa.Cep;
         txtBairro.Text = empresa.Bairro;
         txtCidade.Text = empresa.NomeCidade;
         txtEstado.Text = empresa.UF;
         txtTelefone.Text = empresa.TelefoneCompleto;
         //desabilitar campos para não editar
         txtRazaoSocial.IsEnabled = false;
         txtInscricaoEstadual.IsEnabled = false;
         txtCpfCnpjEmitente.IsEnabled = false;
         txtEndereco.IsEnabled = false;
         txtCep.IsEnabled = false;
         txtBairro.IsEnabled = false;
         txtCidade.IsEnabled = false;
         txtEstado.IsEnabled = false;
         txtTelefone.IsEnabled = false;
      }

      private void DefinirMascaraTipoInscricao(int tipoInscricao, TextBoxCustomizado textbox)
      {
         if (tipoInscricao == (Constants.CPF))
            textbox.TipoControle = TextBoxCustomizado.Tipo.CPF;
         else if (tipoInscricao == (Constants.CNPJ))
            textbox.TipoControle = TextBoxCustomizado.Tipo.CNPJ;
         else
            textbox.TipoControle = TextBoxCustomizado.Tipo.Numerico;
      }
      private void uscClienteDestinatario_EventoCodigoAlterado(object sender, CodigoAlteradoArgs e)
      {
           var destinatario = new ClienteBusiness().BuscarPorId(uscClienteDestinatario.Id.GetValueOrDefault());

         if (destinatario != null)
         {
            cliente = destinatario;
            txtInscricaoEstadualDestinatario.Text = destinatario.InscricaoEstadual;
            txtCpfCnpjDestinatario.Text = destinatario.NumeroInscricao;
            DefinirMascaraTipoInscricao(destinatario.TipoInscricao, txtCpfCnpjDestinatario);
            txtenderecoDestinatario.Text = destinatario.EnderecoTipoRua;
            txtCEPDestinatario.Text = destinatario.CEP;
            txtBairroDestinatario.Text = destinatario.Bairro;
            txtcidadeDestinatario.Text = destinatario.Cidade;
            txtEstadoDestinatario.Text = destinatario.UF;
            txtTelefoneDestinatario.Text = destinatario.TelefoneCompleto;
            txtEmail.Text = destinatario.Email;
            cboContribuinteIcmsDestinatario.SelectedValue = (indIEDest)destinatario.IndicadorIEDestinatario;
            if (cboTipoNota.SelectedItem != null)
               tbiProduto.IsEnabled = true;

            //preencher cfop de acorfo com o estado do cliente
            estadoCliente = new EstadoBusiness().BuscarPorId(destinatario.tb_cidade.idEstado);
            if (cboTipoNota.SelectedValue != null)
            {
               var itemSelecionado = (int)cboTipoNota.SelectedValue;
               if (itemSelecionado == (int)EnumTipoNota.DevolucaoCompra)
               {
                  ConfigurarCFOPDevolucaoCompra(estadoCliente, empresa);
                  cboFinalidade.ItemsSource = EnumHelper.ToList(typeof(FinalidadeNFe)).Where(x => x.Key.ToInt32() == (int)FinalidadeNFe.fnDevolucao);
                  cboFinalidade.SelectedValue = FinalidadeNFe.fnDevolucao;
               }
               else if (itemSelecionado == (int)EnumTipoNota.DevolucaoVenda)
               {
                  ConfigurarCFOPDevolucaoVenda(estadoCliente, empresa);
                  cboFinalidade.ItemsSource = EnumHelper.ToList(typeof(FinalidadeNFe)).Where(x => x.Key.ToInt32() == (int)FinalidadeNFe.fnDevolucao);
                  cboFinalidade.SelectedValue = FinalidadeNFe.fnDevolucao;
               }
               else if (itemSelecionado == (int)EnumTipoNota.Saida)
               {
                  ConfigurarCFOPVenda(estadoCliente, empresa);
                  cboFinalidade.ItemsSource = EnumHelper.ToList(typeof(FinalidadeNFe)).Where(x => x.Key.ToInt32() != (int)FinalidadeNFe.fnDevolucao);
                  cboFinalidade.SelectedValue = FinalidadeNFe.fnNormal;
               }
               else if (itemSelecionado == (int)EnumTipoNota.Entrada)
               {
                  ConfigurarCFOPCompra(estadoCliente, empresa);
                  cboFinalidade.ItemsSource = EnumHelper.ToList(typeof(FinalidadeNFe)).Where(x => x.Key.ToInt32() != (int)FinalidadeNFe.fnDevolucao);
                  cboFinalidade.SelectedValue = FinalidadeNFe.fnNormal;
                  if (cliente.ProdutorRural == true)
                  {
                     tbiReferenciaDevolucao.Visibility = Visibility.Visible;
                     rdbDevolucaoNota.IsEnabled = false;
                     rdbDevolucaoCupom.IsEnabled = false;
                     tbiNfe.IsEnabled = false;
                     tbiCupom.IsEnabled = false;
                     txtchaveReferencia.IsEnabled = false;
                     btnAdicionarChave.IsEnabled = false;
                     tbiReferenciaContraNota.IsEnabled = true;
                     tbiReferenciaContraNota.Focus();
                  }
                  else
                     tbiReferenciaDevolucao.Visibility = Visibility.Collapsed;

               }
            }

            if (empresa.tb_cidade.tb_estado.idPais != destinatario.tb_cidade.tb_estado.idPais && cboTipoNota.SelectedIndex == (int)EnumTipoNota.Entrada)
            {
               tbiImpostoImportacao.Visibility = Visibility.Visible;
               tbiDeclaracaoImportacao.Visibility = Visibility.Visible;
            }
            else
            {
               tbiImpostoImportacao.Visibility = Visibility.Collapsed;
               tbiDeclaracaoImportacao.Visibility = Visibility.Collapsed;
            }
            //Se o cliente for do mesmo estado do emitente não habilita o check box par informar partilha.
            if (cliente.tb_cidade.tb_estado.Sigla == empresa.tb_cidade.tb_estado.Sigla)
               chkPartilha.IsEnabled = false;
            else
               chkPartilha.IsEnabled = true;
         }
         else
         {
            txtInscricaoEstadualDestinatario.Text = string.Empty;
            txtCpfCnpjDestinatario.Text = string.Empty;
            txtenderecoDestinatario.Text = string.Empty;
            txtCEPDestinatario.Text = string.Empty;
            txtBairroDestinatario.Text = string.Empty;
            txtcidadeDestinatario.Text = string.Empty;
            txtEstadoDestinatario.Text = string.Empty;
            txtTelefoneDestinatario.Text = string.Empty;
            txtEmail.Text = string.Empty;
            cboContribuinteIcmsDestinatario.Text = string.Empty;
            tbiProduto.IsEnabled = false;
         }
         //preencher quatidade do produto
         txtQuantidade.Text = "1";
 
      }
      private void CriarClientePorFornecedor(int id)
      {

         if (cboTipoNota.SelectedIndex == (int)EnumTipoNota.DevolucaoCompra)
         {
            var fornecedor = new FornecedorBusiness().BuscarPorId(id);
            if (fornecedor != null)
            {
               var cliente = clienteBusiness.BuscarPorInscricao((EnumTipoInscricao)fornecedor.TipoInscricao, fornecedor.NumeroInscricao);
               if (cliente == null)
                  clienteBusiness.CriarClientePorFornecedor(fornecedor);
            }
         }

      }
      private void DesabilitarCamposDestinatario()
      {
         txtInscricaoEstadualDestinatario.IsEnabled = false;
         txtCpfCnpjDestinatario.IsEnabled = false;
         txtenderecoDestinatario.IsEnabled = false;
         txtCEPDestinatario.IsEnabled = false;
         txtBairroDestinatario.IsEnabled = false;
         txtcidadeDestinatario.IsEnabled = false;
         txtEstadoDestinatario.IsEnabled = false;
         txtTelefoneDestinatario.IsEnabled = false;
         txtEmail.IsEnabled = false;
         cboContribuinteIcmsDestinatario.IsEnabled = false;
      }
      private void cboTipoNota_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         if (nfe != null && e.AddedItems != null)
         {
            nfe.TipoNota = (EnumTipoNota)((KeyValuePair<int, string>)e.AddedItems[0]).Key;
            VerificarTipoNota(nfe.TipoNota);
         }

         if (cboTipoNota.SelectedValue.ToInt32() == (int)EnumTipoNota.DevolucaoVenda)
         {
            tbiReferenciaDevolucao.Visibility = Visibility.Visible;
            rdbDevolucaoCupom.IsEnabled = true;
            tbiReferenciaContraNota.IsEnabled = false;
         }
         if (cboTipoNota.SelectedValue.ToInt32() == (int)EnumTipoNota.DevolucaoCompra)
         {
            if(MessageBoxUtils.ExibeMensagemQuestion("Deseja cadastrar o Fornecedor como Cliente?"))
            { 
               FecharTela();
               EnviarParametro("ConsultaFornecedor", nfe, true);           
            }

            tbiReferenciaDevolucao.Visibility = Visibility.Visible;
            rdbDevolucaoNota.IsEnabled = true;
            txtchaveReferencia.IsEnabled = true;
            btnAdicionarChave.IsEnabled = true;
            rdbDevolucaoCupom.IsEnabled = false;
            tbiReferenciaContraNota.IsEnabled = false;
         }
         if (cboTipoNota.SelectedValue.ToInt32() == (int)EnumTipoNota.Entrada && cliente != null && cliente.ProdutorRural == true)
         {
            tbiReferenciaDevolucao.Visibility = Visibility.Visible;
            tbiReferenciaContraNota.Visibility = Visibility.Visible;
            txtNumeroNotaReferencia.IsEnabled = true;
            txtNumeroSerieReferencia.IsEnabled = true;
            btnAdicionarReferenciaContranota.IsEnabled = true;
            tbiNfe.IsEnabled = false;
            tbiCupom.IsEnabled = false;
            rdbDevolucaoCupom.IsEnabled = false;
            rdbDevolucaoNota.IsEnabled = false;
            tbiReferenciaContraNota.IsEnabled = true;
            txtchaveReferencia.IsEnabled = false;
            btnAdicionarChave.IsEnabled = false;
         }
         else
         {
            txtNumeroNotaReferencia.IsEnabled = false;
            txtNumeroSerieReferencia.IsEnabled = false;
            btnAdicionarReferenciaContranota.IsEnabled = false;
            tbiReferenciaContraNota.Visibility = Visibility.Collapsed;
         }
         if (uscClienteDestinatario.Id != null)
         {
            var itemSelecionado = cboTipoNota.SelectedIndex;
            if (itemSelecionado == (int)EnumTipoNota.DevolucaoCompra)
            {
               ConfigurarCFOPDevolucaoCompra(estadoCliente, empresa);
               cboFinalidade.ItemsSource = EnumHelper.ToList(typeof(FinalidadeNFe)).Where(x => x.Key.ToInt32() == (int)FinalidadeNFe.fnDevolucao);
               cboFinalidade.SelectedValue = FinalidadeNFe.fnDevolucao;
            }
            else if (itemSelecionado == (int)EnumTipoNota.DevolucaoVenda)
            {
               ConfigurarCFOPDevolucaoVenda(estadoCliente, empresa);
               cboFinalidade.ItemsSource = EnumHelper.ToList(typeof(FinalidadeNFe)).Where(x => x.Key.ToInt32() == (int)FinalidadeNFe.fnDevolucao);
               cboFinalidade.SelectedValue = FinalidadeNFe.fnDevolucao;
            }
            else if (itemSelecionado == (int)EnumTipoNota.Saida)
            {
               ConfigurarCFOPVenda(estadoCliente, empresa);
               cboFinalidade.ItemsSource = EnumHelper.ToList(typeof(FinalidadeNFe)).Where(x => x.Key.ToInt32() != (int)FinalidadeNFe.fnDevolucao);
               cboFinalidade.SelectedValue = FinalidadeNFe.fnNormal;
            }
            else if (itemSelecionado == (int)EnumTipoNota.Entrada)
            {
               ConfigurarCFOPCompra(estadoCliente, empresa);
               cboFinalidade.ItemsSource = EnumHelper.ToList(typeof(FinalidadeNFe)).Where(x => x.Key.ToInt32() != (int)FinalidadeNFe.fnDevolucao);
               cboFinalidade.SelectedValue = FinalidadeNFe.fnNormal;
            }


            var destinatario = new ClienteBusiness().BuscarPorId(uscClienteDestinatario.Id.GetValueOrDefault());
            if (empresa.tb_cidade.tb_estado.idPais != destinatario.tb_cidade.tb_estado.idPais && cboTipoNota.SelectedIndex == (int)EnumTipoNota.Entrada)
            {
               tbiImpostoImportacao.Visibility = Visibility.Visible;
               tbiDeclaracaoImportacao.Visibility = Visibility.Visible;
            }
            else
            {
               tbiImpostoImportacao.Visibility = Visibility.Collapsed;
               tbiDeclaracaoImportacao.Visibility = Visibility.Collapsed;
            }
         }

      }
      private void VerificarTipoNota(EnumTipoNota tipoNota)
      {
         if (tipoNota != null)
         {
            var itemSelecionado = tipoNota.ToInt32();
            if (itemSelecionado == (int)EnumTipoNota.DevolucaoCompra && parametro.Industrial == false)
            {
               txtPercentualIPIDevolvido.IsEnabled = true;
               txtIPIDevolvido.IsEnabled = true;
            }
            else
            {
               txtPercentualIPIDevolvido.IsEnabled = false;
               txtIPIDevolvido.IsEnabled = false;
               txtPercentualIPIDevolvido.Text = string.Empty;
               txtIPIDevolvido.Text = string.Empty;
            }
         }
      }

      private void ConfigurarCFOPVenda(tb_estado estado, tb_empresa empresa)
      {
         if (empresa.tb_cidade.idEstado == estado.id)
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.Saida }, EnumOrigemCFOP.Estadual);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.Saida }, EnumOrigemCFOP.Estadual);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
         else if (empresa.tb_cidade.idEstado != estado.id && empresa.tb_cidade.tb_estado.idPais == estado.idPais)
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.Saida }, EnumOrigemCFOP.Interestadual);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.Saida }, EnumOrigemCFOP.Interestadual);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
         else
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.Saida }, EnumOrigemCFOP.Exterior);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.Saida }, EnumOrigemCFOP.Exterior);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
      }

      private void ConfigurarCFOPCompra(tb_estado estado, tb_empresa empresa)
      {
         if (empresa.tb_cidade.idEstado == estado.id)
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.Entrada, EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Estadual);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.Entrada, EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Estadual);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
         else if (empresa.tb_cidade.idEstado != estado.id && empresa.tb_cidade.tb_estado.idPais == estado.idPais)
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.Entrada, EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Interestadual);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.Entrada, EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Interestadual);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
         else
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.Entrada, EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Exterior);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.Entrada, EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Exterior);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
      }

      private void ConfigurarCFOPDevolucaoVenda(tb_estado estado, tb_empresa empresa)
      {
         if (empresa.tb_cidade.idEstado == estado.id)
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Estadual);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Estadual);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
         else if (empresa.tb_cidade.idEstado != estado.id && empresa.tb_cidade.tb_estado.idPais == estado.idPais)
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Interestadual);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Interestadual);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
         else
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Exterior);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoVenda }, EnumOrigemCFOP.Exterior);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
      }

      private void ConfigurarCFOPDevolucaoCompra(tb_estado estado, tb_empresa empresa)
      {
         if (empresa.tb_cidade.idEstado == estado.id)
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoCompra }, EnumOrigemCFOP.Estadual);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoCompra }, EnumOrigemCFOP.Estadual);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
         else if (empresa.tb_cidade.idEstado != estado.id && empresa.tb_cidade.tb_estado.idPais == estado.idPais)
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoCompra }, EnumOrigemCFOP.Interestadual);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoCompra }, EnumOrigemCFOP.Interestadual);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
         else
         {
            new CFOPConfiguracaoHelper().Configurar(uscCFOP, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoCompra }, EnumOrigemCFOP.Exterior);
            new CFOPConfiguracaoHelper().Configurar(uscCFOPMassa, new List<EnumTipoCFOP>() { EnumTipoCFOP.DevolucaoCompra }, EnumOrigemCFOP.Exterior);
            listaCFOPS = new List<tb_cfop>();
            listaCFOPS = uscCFOP.ListaDados as List<tb_cfop>;
         }
      }
      #endregion
      private void ValorTotalProduto()
      {
         decimal valor = txtValorUnitario.Text.ToDecimalOrNull().GetValueOrDefault() *
            txtQuantidade.Text.ToDecimalOrNull().GetValueOrDefault() + +
            txtValorFreteProduto.Text.ToDecimalOrNull().GetValueOrDefault() +
            txtValorSeguro.Text.ToDecimalOrNull().GetValueOrDefault() +
            txtValorDespesas.Text.ToDecimalOrNull().GetValueOrDefault() -
            txtValorDescontoProduto.Text.ToDecimalOrNull().GetValueOrDefault();

         txtValorTotalProduto.Text = valor.ToString("N2");
      }

      //preencher dados do produto e imposto ao inserir produto.
      private void uscProduto_EventoCodigoAlterado(object sender, CodigoAlteradoArgs e)
      {
         if (uscProduto.Id.HasValue)
            produto = new ProdutoBusiness().BuscarPorFiltro("id", uscProduto.Id.Value).FirstOrDefault();
         else
         {
            produto = null;
         }

         //Preencher cfop
         if (produto != null)
         {
            if (cboTipoNota.SelectedValue.ToInt32() != (int)EnumTipoNota.Entrada)
            {
               if (empresa.tb_cidade.idEstado == estadoCliente.id)
                  uscCFOP.Id = produto.idCFOPEstadual;
               else if (empresa.tb_cidade.idEstado != estadoCliente.id && empresa.tb_cidade.tb_estado.idPais == estadoCliente.idPais)
                  uscCFOP.Id = produto.idCFOPInterestadual;
               else
                  uscCFOP.Id = produto.idCFOPExterior;
            }
            else
            {
               if (empresa.tb_cidade.idEstado == estadoCliente.id)
                  uscCFOP.Id = produto.idCFOPEstadualEntrada;
               else if (empresa.tb_cidade.idEstado != estadoCliente.id && empresa.tb_cidade.tb_estado.idPais == estadoCliente.idPais)
                  uscCFOP.Id = produto.idCFOPInterestadualEntrada;
               else
                  uscCFOP.Id = produto.idCFOPExteriorEntrada;
            }

            //preencher valor produto
            if (cboAtacadoVarejo.SelectedIndex == (int)EnumAtacadoVarejo.Atacado)
               txtValorUnitario.Text = produto.ValorVendaAtacado.ToStringOrEmpty("N2");
            else
               txtValorUnitario.Text = produto.ValorVendaVarejo.ToStringOrEmpty("N2");

            //preencher impostos e habilitar campos aba ICMS
            HabilitarAbasImpostos();
            HabilitarAbaPartilhaICMS();
            var produtoImposto = new ProdutoImpostoBusiness().BuscarPorId(produto.id);
            var produtoImpostoInterestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProdutoTo(produto.id);
            //inserir aliquota de icms automaticamente com base na tb_icms_interestadual
            var produtoImpostoInterestadualTabelaIcms = new ICMSInterestadualBusiness().BuscarPorIdOrigemIdDestino(1, empresa.tb_cidade.idEstado, cliente.tb_cidade.idEstado);
            if (produtoImposto != null)
            {
               //preencher icms e icmsst dentro do estado ou buscar dados na tabela interestadual
               if (estadoCliente.id == empresa.tb_cidade.idEstado && estadoCliente.tb_pais.id == 30 || produtoImpostoInterestadual == null)
               {
                  txtPorcentagemReducao.Text = produtoImposto.PorcentagemReducao.ToStringOrEmpty("N2");
                  CalcularBaseIcms();
                  uscOrigem.Id = produtoImposto.idOrigemMercadoria;

                  //quando o produto for selecionado no grid de produtos da nfe vai preencher os ids com os dados do grid
                  produtoImposto.idCSTICMS = produtoSelecionado != null ? produtoSelecionado.idCSTICMS : produtoImposto.idCSTICMS;
                  produtoImposto.idCSOSN = produtoSelecionado != null ? produtoSelecionado.idCSOSN : produtoImposto.idCSOSN;

                  if (cliente.ConsumidorFinal && produtoImposto.idCSTICMS.In(10, 30, 50, 51, 70, 90))
                  {
                     MessageBoxUtils.ExibeMensagemAdvertencia("CST/CSOSN não é compatível com o cliente consumidor final!");
                  }
                  else if (cliente.ConsumidorFinal && produtoImposto.idCSOSN.In(101, 201, 202, 203, 900))
                  {
                     MessageBoxUtils.ExibeMensagemAdvertencia("CST/CSOSN não é compatível com o cliente consumidor final!");
                  }
                  else
                  {
                     if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
                        uscCSTICMS.Id = produtoImposto.idCSOSN.GetValueOrDefault();
                     else
                        uscCSTICMS.Id = produtoImposto.idCSTICMS.GetValueOrDefault();
                  }
                  if (produtoImposto.idCSTICMS.In(0, 20, 70, 90, 51) || produtoImposto.idCSOSN.In(201, 202, 203))
                     txtPorcentagemReducao.IsEnabled = true;
                  else
                     txtPorcentagemReducao.IsEnabled = false;
                  if (produtoImposto.idCSTICMS.In(00, 10, 20, 70, 90, 51) || produtoImposto.idCSOSN.In(101, 201, 202, 203, 900))
                  {
                     txtAliquota.IsEnabled = true;
                     //define se busca a aliquota da tabela ou aliquota cadastrada manualmente
                     if (estadoCliente.id == empresa.tb_cidade.idEstado)
                        txtAliquota.Text = produtoImposto.AliquotaICMS.ToStringOrNull("N2");
                     else if (produtoImpostoInterestadualTabelaIcms != null)
                        txtAliquota.Text = produtoImpostoInterestadualTabelaIcms.PorcentagemAliquota.ToString("N2");
                  }
                  else
                     txtAliquota.IsEnabled = false;

                  txtPorcentagemDiferimento.Text = produtoImposto.PorcentagemDiferimento.ToStringOrNull("N2");

                  if (txtAliquota.Text != null)
                     CalcularICMS();

                  //preencher impostos aba ICMSST dentro do estado
                  if (produtoImposto.idCSTICMS.In(10, 70, 90) || produtoImposto.idCSOSN.In(201, 202, 203, 900))
                  {
                     tbiICMSST.Visibility = Visibility.Visible;
                     txtPorcentagemST.Text = produtoImposto.PorcentagemReducao.ToStringOrNull("N2");
                     txtMVA.Text = produtoImposto.MVA.ToStringOrNull("N2");
                     if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
                        txtAliquotaICMSST.Text = produtoImposto.AliquotaST.ToStringOrNull("N2");
                     else
                        txtAliquotaICMSST.Text = produtoImposto.AliquotaICMS.ToStringOrNull("N2");
                     txtBaseICMSST.Text = txtBaseIcms.Text;
                     if (txtAliquotaICMSST.Text != null)
                        CalcularICMSST();
                  }
                  else
                     tbiICMSST.Visibility = Visibility.Collapsed;

               }
               //preencher icms e icmsst fora do estado
               else if (produtoImpostoInterestadual != null)
               {
                  txtPorcentagemReducao.Text = produtoImpostoInterestadual.PorcentagemReducao.ToStringOrEmpty("N2");
                  CalcularBaseIcms();
                  uscOrigem.Id = produtoImposto.idOrigemMercadoria;

                  produtoImpostoInterestadual.idCST = produtoSelecionado != null ? produtoSelecionado.idCSTICMS : produtoImposto.idCSTICMS;
                  produtoImpostoInterestadual.idCSOSN = produtoSelecionado != null ? produtoSelecionado.idCSOSN : produtoImposto.idCSOSN;

                  if (cliente.ConsumidorFinal && produtoImpostoInterestadual.idCST.In(10, 30, 50, 51, 70, 90))
                  {
                     MessageBoxUtils.ExibeMensagemAdvertencia("CST/CSOSN não é compatível com o cliente consumidor final!");
                  }
                  else if (cliente.ConsumidorFinal && produtoImpostoInterestadual.idCSOSN.In(101, 201, 202, 203, 900))
                  {
                     MessageBoxUtils.ExibeMensagemAdvertencia("CST/CSOSN não é compatível com o cliente consumidor final!");
                  }
                  else
                  {
                     if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
                        uscCSTICMS.Id = produtoImpostoInterestadual.idCSOSN.GetValueOrDefault();
                     else
                        uscCSTICMS.Id = produtoImpostoInterestadual.idCST.ToIntOrNull();
                  }
                  if (produtoImpostoInterestadual.idCST.In(0, 20, 70, 90, 51) || produtoImpostoInterestadual.idCSOSN.In(201, 202, 203))
                     txtPorcentagemReducao.IsEnabled = true;
                  else
                     txtPorcentagemReducao.IsEnabled = false;
                  if (produtoImposto.idCSTICMS.In(00, 10, 20, 70, 90, 51) || produtoImposto.idCSOSN.In(101, 201, 202, 203, 900))
                  {
                     txtAliquota.IsEnabled = true;
                     txtAliquota.Text = produtoImpostoInterestadual.Aliquota.ToStringOrNull("N2");
                  }
                  else
                     txtAliquota.IsEnabled = false;

                  txtPorcentagemDiferimento.Text = produtoImpostoInterestadual.PorcentagemDiferimento.ToStringOrNull("N2");

                  if (txtAliquota.Text != null)
                     CalcularICMS();

                  //preencher impostos aba ICMSST fora do estado
                  if (!cliente.ConsumidorFinal && produtoImpostoInterestadual.idCST.In(10, 30, 60, 70, 90) || produtoImpostoInterestadual.idCSOSN.In(201, 202, 203, 900))
                  {
                     txtPorcentagemST.Text = produtoImpostoInterestadual.PorcentagemReducao.ToStringOrNull("N2");
                     txtMVA.Text = produtoImpostoInterestadual.MVA.ToStringOrNull("N2");
                     txtAliquotaICMSST.Text = produtoImpostoInterestadual.Aliquota.ToStringOrNull("N2");
                     txtBaseICMSST.Text = txtBaseIcms.Text;
                     if (txtAliquotaICMSST.Text != null)
                        CalcularICMSST();
                  }
                  else
                     tbiICMSST.Visibility = Visibility.Collapsed;
               }

               //preencher aba PIS
               if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional && produtoImposto.idCSTPISCOFINS.In(1, 2, 3, 49, 99))
               {
                  tbiPIS.Visibility = Visibility.Visible;

                  if (TipoNota == (int)EnumTipoNota.Saida || TipoNota == (int)EnumTipoNota.DevolucaoCompra)
                  {
                     uscCSTPIS.Id = produtoImposto.idCSTPISCOFINS.GetValueOrDefault();
                     txtAliquotaPIS.Text = produtoImposto.AliquotaPIS.ToStringOrNull("N2");
                  }
                  else
                  {
                     uscCSTPIS.Id = produtoImposto.idCSTPISCOFINSEntrada.GetValueOrDefault();
                     txtAliquotaPIS.Text = produtoImposto.AliquotaPISEntrada.ToStringOrNull("N2");
                  }
                  if (produtoImposto.AliquotaPISEntrada != null || produtoImposto.AliquotaPIS != null)
                     CalcularBasePis();
               }
               else
                  tbiPIS.Visibility = Visibility.Collapsed;

               //preencher aba COFINS
               if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional && produtoImposto.idCSTPISCOFINS.In(1, 2, 3, 49, 99))
               {
                  tbiCOFINS.Visibility = Visibility.Visible;
                  if (TipoNota == (int)EnumTipoNota.Saida || TipoNota == (int)EnumTipoNota.DevolucaoCompra)
                  {
                     uscCSTCOFINS.Id = produtoImposto.idCSTPISCOFINS.GetValueOrDefault();
                     txtAliquotaCOFINS.Text = produtoImposto.AliquotaCOFINS.ToStringOrNull("N2");
                  }
                  else
                  {
                     uscCSTCOFINS.Id = produtoImposto.idCSTPISCOFINSEntrada.GetValueOrDefault();
                     txtAliquotaCOFINS.Text = produtoImposto.AliquotaCOFINSEntrada.ToStringOrNull("N2");
                  }
                  if (produtoImposto.AliquotaCOFINS != null || produtoImposto.AliquotaCOFINS != null)
                     CalcularBaseCofins();
               }

               else
                  tbiCOFINS.Visibility = Visibility.Collapsed;

               //preencher aba IPI
               if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional && parametro.Industrial && !cliente.ConsumidorFinal)
               {
                  tbiIPI.Visibility = Visibility.Visible;
                  if (TipoNota == (int)EnumTipoNota.Saida || TipoNota == (int)EnumTipoNota.DevolucaoCompra)
                  {
                     uscCSTIPI.Id = produtoImposto.idCSTIPISaida.GetValueOrDefault();
                     txtAliquotaIPI.Text = produtoImposto.AliquotaIPISaida.ToStringOrNull("N2");
                  }
                  else
                  {
                     uscCSTIPI.Id = produtoImposto.idCSTIPIEntrada.GetValueOrDefault();
                     txtAliquotaIPI.Text = produtoImposto.AliquotaIPIEntrada.ToStringOrNull("N2");
                  }
                  if (produtoImposto.AliquotaIPISaida != null || produtoImposto.AliquotaIPIEntrada != null)
                     CalcularBaseIpi();
               }
               else
                  tbiIPI.Visibility = Visibility.Collapsed;

               if (empresa.tb_cidade.tb_estado.idPais != cliente.tb_cidade.tb_estado.idPais && cboTipoNota.SelectedIndex == (int)EnumTipoNota.Entrada)
               {
                  tbiImpostoImportacao.Visibility = Visibility.Visible;
                  tbiDeclaracaoImportacao.Visibility = Visibility.Visible;
               }
               else
               {
                  tbiImpostoImportacao.Visibility = Visibility.Collapsed;
                  tbiDeclaracaoImportacao.Visibility = Visibility.Collapsed;
               }

               //partilha icms
               CalcularPartilhaICMS(0, produto);
            }

         }
         if (produto == null)
         {
            Utilidades.LimparControles(tbcProduto);
            txtQuantidade.Text = "1";
         }
         ValorTotalProduto();
      }
      private void HabilitarAbasImpostos()
      {
         parametro = new ParametroBusiness().BuscarParametroVigente();
         uscOrigem.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
         uscCSTICMS.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
         if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
         {
            new CSOSNConfiguracaoHelper().Configurar(uscCSTICMS);
            lblCSOSNCST.Text = "CSOSN:";

            tbiICMS.Visibility = Visibility.Visible;
            tbiPIS.Visibility = Visibility.Collapsed;
            tbiCOFINS.Visibility = Visibility.Collapsed;
            tbiIPI.Visibility = Visibility.Collapsed;
            uscCSTICMS.Tag = "CSOSN";
         }
         else
         {
            new CSTICMSConfiguracaoHelper().Configurar(uscCSTICMS);
            lblCSOSNCST.Text = "CST ICMS:";
            tbiICMS.Visibility = Visibility.Visible;
            tbiPIS.Visibility = Visibility.Visible;
            tbiCOFINS.Visibility = Visibility.Visible;
            uscCSTICMS.Tag = "CST";
            uscCSTPIS.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            uscCSTCOFINS.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            if (parametro.Industrial)
            {
               tbiIPI.Visibility = Visibility.Visible;
               uscCSTIPI.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            }
         }
      }
      private void HabilitarAbaPartilhaICMS()
      {
         //Partilha ICMS
         if (uscClienteDestinatario.Id.HasValue)
         {
            if (chkPartilha.IsChecked == true)
            {
               tbiPartilhaICMS.Visibility = Visibility.Visible;
            }
            else
            {
               tbiPartilhaICMS.Visibility = Visibility.Collapsed;
            }
         }
      }
      private void txtValorDescontoProduto_TextChanged(object sender, TextChangedEventArgs e)
      {
         ValorTotalProduto();
         CalcularBaseIcms();
         CalcularBasePis();
         CalcularBaseCofins();
         CalcularBaseIpi();
      }

      private void txtValorUnitario_TextChanged(object sender, TextChangedEventArgs e)
      {
         ValorTotalProduto();
         CalcularBaseIcms();
         CalcularBasePis();
         CalcularBaseCofins();
         CalcularBaseIpi();
         if (tbiPartilhaICMS.Visibility == Visibility.Visible)
            CalcularPartilhaICMS(0, produto);
      }

      private void txtValorFreteProduto_TextChanged(object sender, TextChangedEventArgs e)
      {
         ValorTotalProduto();
         CalcularBaseIcms();
         CalcularBasePis();
         CalcularBaseCofins();
         CalcularBaseIpi();
      }

      private void txtValorDespesas_TextChanged(object sender, TextChangedEventArgs e)
      {
         ValorTotalProduto();
         CalcularBaseIcms();
         CalcularBasePis();
         CalcularBaseCofins();
         CalcularBaseIpi();
      }

      private void txtValorSeguro_TextChanged(object sender, TextChangedEventArgs e)
      {
         ValorTotalProduto();
         CalcularBaseIcms();
         CalcularBasePis();
         CalcularBaseCofins();
         CalcularBaseIpi();
      }

      private void txtQuantidade_TextChanged(object sender, TextChangedEventArgs e)
      {
         ValorTotalProduto();
         CalcularBaseIcms();
         CalcularBasePis();
         CalcularBaseCofins();
         CalcularBaseIpi();
         if (tbiPartilhaICMS.Visibility == Visibility.Visible)
            CalcularPartilhaICMS(0, produto);
      }

      private void btnAdicionarProduto_Click(object sender, RoutedEventArgs e)
      {
         if (uscProduto.Id == null)
            throw new BusinessException("É necessário inserir pelo menos um produto.");

         decimal Quantidade = txtQuantidade.Text.ToDecimal();
         if (Quantidade <= 0)
            throw new BusinessException("É necessário inserir a quantidade maior que zero! ");

         decimal valorMVA = txtMVA.Text.ToDecimal();
         if (valorMVA > 100)
            throw new BusinessException("Valor do MVA Inválido!");

         decimal valorAliquotaICMS = txtAliquota.Text.ToDecimal();
         if (valorAliquotaICMS > 99)
            throw new BusinessException("Valor da Aliquota de ICMS Inválida!");

         decimal valorAliquotaIcmsSt = txtValorICMSST.Text.ToDecimal();
         if (valorAliquotaIcmsSt < 0)
            throw new BusinessException("Valor do ICMSST Inválido!");

         tb_nfe_produto nfeProduto = produtoSelecionado;

         if (nfeProduto == null || !nfeProdutoObservable.Where(x => x.id == nfeProduto.id).Any())
            nfeProduto = new tb_nfe_produto();

         if (btnAdicionarProduto.Tag == "Salvar")
         {
            pagamentoObservable.Clear();
            formaPagamentoObservable.Clear();
         }
         //Geral
         nfeProduto.idNFe = nfe.id;
         nfeProduto.tb_produto = uscProduto.DataContext as tb_produto;
         if (!VerificarNCM())
            return;
         nfeProduto.tb_produto.tb_unidade = new UnidadeBusiness().BuscarPorId(nfeProduto.tb_produto.idUnidade);
         if (nfeProduto.tb_produto.idMarca.HasValue)
            nfeProduto.tb_produto.tb_marca_produto = new MarcaProdutoBusiness().BuscarPorId(nfeProduto.tb_produto.idMarca.Value);
         nfeProduto.Quantidade = txtQuantidade.Text.ToDecimal();
         nfeProduto.idProduto = uscProduto.Id.Value;
         nfeProduto.ValorDesconto = txtValorDescontoProduto.Text.ToStringReplaceMonetario().ToDecimalOrNull();
         nfeProduto.ValorUnitario = txtValorUnitario.Text.ToStringReplaceMonetario().ToDecimal();
         decimal valorIPI = txtValorIPI.Text.ToDecimal();
         decimal valorICMSST = txtValorICMSST.Text.ToDecimal();
         decimal valorTotalProduto = txtValorTotalProduto.Text.ToString().ToDecimal();
         nfeProduto.ValorTotalProduto = valorTotalProduto + valorICMSST + valorIPI;
         nfeProduto.ValorFrete = txtValorFreteProduto.Text.ToStringReplaceMonetario().ToDecimalOrNull();
         nfeProduto.ValorSeguro = txtValorSeguro.Text.ToStringReplaceMonetario().ToDecimalOrNull();
         nfeProduto.ValorOutrasDespesas = txtValorDespesas.Text.ToStringReplaceMonetario().ToDecimalOrNull();
         nfeProduto.idCFOP = uscCFOP.Id;
         nfeProduto.tb_cfop = uscCFOP.DataContext as tb_cfop;
         nfeProduto.MVA = txtMVA.Text.ToDecimalOrNull();
         nfeProduto.PorcentagemReducao = txtPorcentagemReducao.Text.ToDecimalOrNull();
         nfeProduto.PorcentagemReducaoST = txtPorcentagemST.Text.ToDecimalOrNull();
         if (tbiICMSST.Visibility == Visibility.Visible)
            nfeProduto.ValorAliquotaST = txtAliquotaICMSST.Text.ToDecimalOrNull();

         if (tbiIcmsAntecipado.Visibility == Visibility.Visible)
         {
            nfeProduto.ValorBaseCalculoICMSSTRet = txtBaseIcmsStAnt.Text.ToDecimalOrNull();
            nfeProduto.ValorAliquotaSTRet = txtPercentualIcmsStAnt.Text.ToDecimalOrNull();
            nfeProduto.ValorICMSSubstituto = txtValorIcmsAnt.Text.ToDecimalOrNull();
            nfeProduto.ValorICMSSTRetido = txtValorICMSSTAnt.Text.ToDecimalOrNull();
            nfeProduto.PorcentagemFCPRet = txtpercentualFCPAnt.Text.ToDecimalOrNull();
         }
         var NCM = new ProdutoBusiness().BuscarPorId(nfeProduto.tb_produto.id);
         if (NCM != null)
            nfeProduto.tb_produto.NCM = NCM.NCM;
         //replace para retirar caracteres especias quando salvar o complemento da descrição do produto.
         nfeProduto.ComplementoDescricao = txtComplementoDescricao.Text.LimparTextoComEspaco().Trim();
         nfeProduto.PorcentagemDiferimento = txtPorcentagemDiferimento.Text.ToDecimalOrNull();
         nfeProduto.Pedido = txtPedido.Text.ToString();
         nfeProduto.CodPedido = txtcodigopedido.Text.ToInt32();
         decimal valorIpiDev = txtPercentualIPIDevolvido.Text.ToDecimal();
         decimal percentualIpiDev = txtIPIDevolvido.Text.ToDecimal();

         if (valorIpiDev > 0 && percentualIpiDev > 0)
         {
            nfeProduto.PercentualIPIDevolv = txtPercentualIPIDevolvido.Text.ToDecimal();
            nfeProduto.ValorIPIDevolv = txtIPIDevolvido.Text.ToDecimal();
         }
         else if (valorIpiDev > 0 && percentualIpiDev == 0)
            throw new BusinessException("Favor preencher o campo IPI Dev!");
         else if (valorIpiDev == 0 && percentualIpiDev > 0)
            throw new BusinessException("Favor preencher o campo %IPI Devolv!");
         else
         {
            nfeProduto.PercentualIPIDevolv = null;
            nfeProduto.ValorIPIDevolv = null;
         }

         //ICMS
         nfeProduto.idOrigemMercadoria = uscOrigem.Id;
         if (uscCSTICMS.Tag == "CST")
         {
            nfeProduto.idCSTICMS = uscCSTICMS.Id;
            if (uscCSTICMS.Id.In(0, 10, 20, 51, 60, 70, 90))
               nfeProduto.ValorBaseCalculoICMS = txtBaseIcms.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.ValorAliquotaICMS = txtAliquota.Text.ToDecimalOrNull();
            nfeProduto.ValorICMS = txtValorICMS.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            //diferimento
            if (uscCSTICMS.Id == 51)
            {
               nfeProduto.ValorICMS = txtValorIcmsAposDiferimento.Text.ToDecimal();
               nfeProduto.ValorIcmsDiferido = txtValorIcmsDiferido.Text.ToDecimal();
            }
         }
         else
         {
            nfeProduto.idCSOSN = uscCSTICMS.Id;
            nfeProduto.ValorBaseCalculoICMS = txtBaseIcms.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.AliquotaCreditoICMS = txtAliquota.Text.ToDecimalOrNull();
            nfeProduto.ValorCreditoICMS = txtValorICMS.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.ValorICMS = txtValorICMS.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.ValorAliquotaICMS = txtAliquota.Text.ToDecimalOrNull();
         }

         //ICMS ST
         if (tbiICMSST.Visibility == Visibility.Visible)
         {
            nfeProduto.ValorBaseCalculoICMSST = txtBaseICMSST.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.ValorICMSST = txtValorICMSST.Text.ToStringReplaceMonetario().ToDecimalOrNull();
         }
         else
         {
            nfeProduto.ValorBaseCalculoICMSST = null;
            nfeProduto.ValorICMSST = null;
         }

         //PIS
         nfeProduto.idCSTPIS = uscCSTPIS.Id;
         if (uscCSTPIS.Id.HasValue)
         {
            nfeProduto.ValorBaseCalculoPIS = txtBasePIS.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.ValorAliquotaPIS = txtAliquotaPIS.Text.ToDecimalOrNull();
            nfeProduto.ValorPIS = txtValorPIS.Text.ToStringReplaceMonetario().ToDecimalOrNull();
         }
         else
         {
            nfeProduto.ValorBaseCalculoPIS = null;
            nfeProduto.ValorAliquotaPIS = null;
            nfeProduto.ValorPIS = null;
         }

         //COFINS
         nfeProduto.idCSTCOFINS = uscCSTCOFINS.Id;
         if (uscCSTCOFINS.Id.HasValue)
         {
            nfeProduto.ValorBaseCalculoCOFINS = txtBaseCOFINS.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.ValorAliquotaCOFINS = txtAliquotaCOFINS.Text.ToDecimalOrNull();
            nfeProduto.ValorCOFINS = txtValorCOFINS.Text.ToStringReplaceMonetario().ToDecimalOrNull();
         }
         else
         {
            nfeProduto.ValorBaseCalculoCOFINS = null;
            nfeProduto.ValorAliquotaCOFINS = null;
            nfeProduto.ValorCOFINS = null;
         }

         //IPI
         nfeProduto.idCSTIPI = uscCSTIPI.Id;
         if (uscCSTIPI.Id.HasValue)
         {
            nfeProduto.ValorBaseCalculoIPI = txtBaseIPI.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.ValorAliquotaIPI = txtAliquotaIPI.Text.ToDecimalOrNull();
            nfeProduto.ValorIPI = txtValorIPI.Text.ToStringReplaceMonetario().ToDecimalOrNull();
         }
         else
         {
            nfeProduto.ValorBaseCalculoIPI = null;
            nfeProduto.ValorAliquotaIPI = null;
            nfeProduto.ValorIPI = null;
         }

         //Partilha ICMS
         //CalcularPartilhaICMS(0, produto);
         if (partilhaICMS != null && tbiPartilhaICMS.Visibility == Visibility.Visible)
         {
            nfeProduto.idPartilhaICMS = partilhaICMS.id;
            nfeProduto.ValorBaseCalculoPartilha = txtBasePartilhaICMS.Text.ToDecimalOrNull();
            nfeProduto.PorcentagemFCP = txtPorcentagemFCP.Text.ToDecimalOrNull();
            nfeProduto.ValorFCP = txtValorFCP.Text.ToDecimalOrNull();
            nfeProduto.PorcentagemInterestadual = this.aliquotaInterestadual;

            if (nfeProduto.ValorAliquotaICMS.GetValueOrDefault() > 0)
               nfeProduto.PorcentagemIntraestadual = nfeProduto.ValorAliquotaICMS;
            else if (nfeProduto.ValorAliquotaST.GetValueOrDefault() > 0)
               nfeProduto.PorcentagemIntraestadual = nfeProduto.ValorAliquotaST.GetValueOrDefault();
            else
               nfeProduto.PorcentagemIntraestadual = this.aliquotaIntraestadual;

            nfeProduto.ValorInterestadual = txtValorICMSOrigem.Text.ToDecimalOrNull();
            nfeProduto.PorcentagemOrigemPartilha = txtPorcentagemOrigem.Text.ToDecimalOrNull();
            nfeProduto.ValorIntraestadual = txtValorDestino.Text.ToDecimalOrNull();
            nfeProduto.PorcentagemDestinoPartilha = txtPorcentagemDestino.Text.ToDecimalOrNull();
         }
         else
         {
            nfeProduto.idPartilhaICMS = null;
            nfeProduto.ValorBaseCalculoPartilha = null;
            nfeProduto.PorcentagemFCP = null;
            nfeProduto.ValorFCP = null;
            nfeProduto.PorcentagemInterestadual = null;
            nfeProduto.PorcentagemIntraestadual = null;
            nfeProduto.ValorInterestadual = null;
            nfeProduto.PorcentagemOrigemPartilha = null;
            nfeProduto.ValorIntraestadual = null;
            nfeProduto.PorcentagemDestinoPartilha = null;
         }

         ValidarCampos();
         ValidarDesconto(nfeProduto.tb_produto);
         ValidarCamposICMS(nfeProduto);
         ValidarCamposObrigatoriosProdutos();
         //Preenche dados da importação
         ObterDadosImportacao(nfeProduto);

         //Validar complemento da descrição
         if (btnAdicionarProduto.Tag == "Adicionar")
            if (!nfeProdutoObservable.Where(x => x.idProduto == uscProduto.Id && x.ComplementoDescricao == txtComplementoDescricao.Text).Any())
               nfeProdutoObservable.Add(nfeProduto);
            else
               throw new BusinessException("Não é possível adicionar dois produtos iguais com o mesmo complemento da descrição.");

         //contar numero de produtos no grid
         int contador = 1;
         int? numeroItem = 0;

         foreach (var produto in nfeProdutoObservable.ToList())
         {
            produto.NumeroItem = contador;
            contador++;

            if (nfeProduto.NumeroItem == 1)
               numeroItem = nfeProduto.NumeroItem;
         }
         //setar a natureza de operação
         if (string.IsNullOrEmpty(txtNaturezaOperacao.Text) || numeroItem == 1)
         {
            var cfop = new CFOPBusiness().BuscarPorId(nfeProdutoObservable.FirstOrDefault().idCFOP.Value);
            txtNaturezaOperacao.Text = cfop.Descricao;
         }

         dgProdutos.ItemsSource = null;
         dgProdutos.ItemsSource = nfeProdutoObservable;
         btnExcluirProduto.Visibility = Visibility.Collapsed;

         CalcularTotais(null);
         // HabilitarAbaReferenciaCupom();

         InformacoesObrigatorias(nfeProdutoObservable);

         LimparCamposProduto();

         btnCalcularDescontoTotal.IsEnabled = true;
         tbiPagamento.IsEnabled = true;
      }
      private void LimparCamposProduto()
      {
         //Limpa todos os códigos para não bugar o controle no próximo carregamento
         uscCFOP.Id = null;
         uscCSTCOFINS.Id = null;
         uscCSTICMS.Id = null;
         uscCSTIPI.Id = null;
         uscCSTPIS.Id = null;
         uscOrigem.Id = null;
         txtValorTotalProduto.Text = "R$ 0,00";

         Utilidades.LimparControles(tbcProduto);

         btnAdicionarProduto.Content = "Adicionar";
         btnAdicionarProduto.Tag = "Adicionar";

         produtoSelecionado = null;
         DesabilitarAbasImpostos();
         tbiDadosProduto.Focus();
         dgProdutos.ItemsSource = null;
         dgProdutos.ItemsSource = nfeProdutoObservable;
         txtQuantidade.Text = "1";
      }
      private void DesabilitarAbasImpostos()
      {
         tbiICMS.Visibility = Visibility.Collapsed;
         tbiICMSST.Visibility = Visibility.Collapsed;
         tbiPartilhaICMS.Visibility = Visibility.Collapsed;
         tbiCOFINS.Visibility = Visibility.Collapsed;
         tbiPIS.Visibility = Visibility.Collapsed;
         tbiIPI.Visibility = Visibility.Collapsed;
         tbiDeclaracaoImportacao.Visibility = Visibility.Collapsed;
         tbiImpostoImportacao.Visibility = Visibility.Collapsed;
      }
      private void ValidarCamposObrigatoriosProdutos()
      {

         if (tbiICMSST.Visibility == Visibility.Collapsed)
         {
            txtMVA.MensagemObrigatoria = string.Empty;
            txtAliquotaICMSST.MensagemObrigatoria = string.Empty;
            txtBaseICMSST.MensagemObrigatoria = string.Empty;
         }
         else
         {
            txtMVA.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtAliquotaICMSST.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtBaseICMSST.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
         }
         if (tbiPIS.Visibility == Visibility.Visible && uscCSTPIS.Id.In(1, 2, 3, 49, 99))
         {
            txtBasePIS.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtAliquotaPIS.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtValorPIS.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
         }
         else
         {
            uscCSTPIS.MensagemObrigatoria = string.Empty;
            txtBasePIS.MensagemObrigatoria = string.Empty;
            txtAliquotaPIS.MensagemObrigatoria = string.Empty;
            txtValorPIS.MensagemObrigatoria = string.Empty;
         }
         if (tbiCOFINS.Visibility == Visibility.Visible && uscCSTCOFINS.Id.In(1, 2, 3, 49, 99))
         {
            txtBaseCOFINS.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtAliquotaCOFINS.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtValorCOFINS.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
         }
         else
         {
            uscCSTCOFINS.MensagemObrigatoria = string.Empty;
            txtBaseCOFINS.MensagemObrigatoria = string.Empty;
            txtAliquotaCOFINS.MensagemObrigatoria = string.Empty;
            txtValorCOFINS.MensagemObrigatoria = string.Empty;
         }
         if (tbiIPI.Visibility == Visibility.Visible && uscCSTIPI.Id.In(50, 99))
         {
            txtBaseIPI.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtAliquotaIPI.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtValorIPI.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
         }
         else
         {
            uscCSTIPI.MensagemObrigatoria = string.Empty;
            txtBaseIPI.MensagemObrigatoria = string.Empty;
            txtAliquotaIPI.MensagemObrigatoria = string.Empty;
            txtValorIPI.MensagemObrigatoria = string.Empty;
         }

         if (tbiDeclaracaoImportacao.Visibility == Visibility.Visible)
         {
            txtNumeroDI.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtDataDelacarao.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtLocalDesembarque.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            cboUFDesembarque.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtDataDesembarque.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            cboTipoViaTransporte.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtValorAFRMM.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            cboFormaImportacao.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtCNPJAdquirente.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            cboUFAdquirente.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtCodigoExportador.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            //aba imposto importação
            txtBaseCalculoII.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtValorII.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtDespesasAduaneiras.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtValorIOF.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
         }

         else
         {
            txtNumeroDI.MensagemObrigatoria = string.Empty;
            txtDataDelacarao.MensagemObrigatoria = string.Empty;
            txtLocalDesembarque.MensagemObrigatoria = string.Empty;
            cboUFDesembarque.MensagemObrigatoria = string.Empty;
            txtDataDesembarque.MensagemObrigatoria = string.Empty;
            cboTipoViaTransporte.MensagemObrigatoria = string.Empty;
            txtValorAFRMM.MensagemObrigatoria = string.Empty;
            cboFormaImportacao.MensagemObrigatoria = string.Empty;
            txtCNPJAdquirente.MensagemObrigatoria = string.Empty;
            cboUFAdquirente.MensagemObrigatoria = string.Empty;
            txtCodigoExportador.MensagemObrigatoria = string.Empty;
            //aba imposto importação
            txtBaseCalculoII.MensagemObrigatoria = string.Empty;
            txtValorII.MensagemObrigatoria = string.Empty;
            txtDespesasAduaneiras.MensagemObrigatoria = string.Empty;
            txtValorIOF.MensagemObrigatoria = string.Empty;
         }
         if (tbiIcmsAntecipado.Visibility == Visibility.Visible)
         {
            txtValorIcmsAnt.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtBaseIcmsStAnt.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtValorICMSSTAnt.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtPercentualIcmsStAnt.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
            txtpercentualFCPAnt.MensagemObrigatoria = Constants.MENSAGEM_CAMPO_OBRIGATORIO;
         }
         else
         {
            txtValorIcmsAnt.MensagemObrigatoria = string.Empty;
            txtBaseIcmsStAnt.MensagemObrigatoria = string.Empty;
            txtValorICMSSTAnt.MensagemObrigatoria = string.Empty;
            txtPercentualIcmsStAnt.MensagemObrigatoria = string.Empty;
            txtpercentualFCPAnt.MensagemObrigatoria = string.Empty;
         }

         ControleNavegador navegador = new ControleNavegador();
         Utilidades.ValidarCamposObrigatorios(tbcProduto, navegador);
         if (navegador.ExisteErroRegistro)
            throw new BusinessException(PCInfo.Utils.Constants.CAMPOS_OBRIGATORIOS);
      }
      private bool ValidarDesconto(tb_produto produto)
      {
         decimal valorDesconto = 0;
         decimal.TryParse(txtValorDescontoProduto.Text.ToStringReplaceMonetario(), out valorDesconto);
         decimal quantidade = 0;
         decimal.TryParse(txtQuantidade.Text.ToStringReplaceMonetario(), out quantidade);
         decimal valorUnitario = 0;
         decimal.TryParse(txtValorUnitario.Text.ToStringReplaceMonetario(), out valorUnitario);

         if (valorDesconto > (valorUnitario * quantidade))
         {
            throw new BusinessException("O valor desconto não pode ser maior que o valor do Produto.");
         }
         else if (produto.DescontoMaximoFixo)
         {
            if (produto.ValorDescontoMaximo != null && produto.DescontoMaximoFixo == true && valorDesconto > produto.ValorDescontoMaximo)
            {
               throw new BusinessException("O desconto máximo para esse produto é " + produto.ValorDescontoMaximo.Value.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO));
            }
            if (produto.ValorDescontoMaximo != null && produto.DescontoMaximoFixo == false && valorDesconto > ((valorUnitario * quantidade) * (produto.ValorDescontoMaximo / 100)))
            {
               var valorMaximo = ((valorUnitario * quantidade) * (produto.ValorDescontoMaximo / 100));
               throw new BusinessException("A porcentagem de desconto máxima para esse produto é  " + produto.ValorDescontoMaximo.Value.ToString("N") + "%\n" + valorMaximo.Value.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO) + ".");
            }
         }
         return true;
      }
      private void ValidarCamposICMS(tb_nfe_produto produto)
      {
         if (produto != null)
         {
            if (produto.idCSOSN.In(101, 201, 900) || produto.idCSTICMS.In(00, 10, 20, 51, 70, 90))
            {
               if (produto.ValorBaseCalculoICMS <= 0)
               {
                  txtBaseIcms.Style = FindResource("boldStyle") as Style;
                  throw new BusinessException(Constants.CAMPOS_OBRIGATORIOS);
               }
               if (produto.ValorAliquotaICMS <= 0)
               {
                  txtAliquota.Style = FindResource("boldStyle") as Style;
                  throw new BusinessException(Constants.CAMPOS_OBRIGATORIOS);
               }
            }
            if (produto.idCSTICMS.In(20, 70) && produto.PorcentagemReducao <= 0)
            {
               txtPorcentagemReducao.Style = FindResource("boldStyle") as Style;
               throw new BusinessException(Constants.CAMPOS_OBRIGATORIOS);
            }
            if (produto.idCSTICMS.In(51) && produto.PorcentagemDiferimento <= 0)
            {
               txtPorcentagemDiferimento.Style = FindResource("boldStyle") as Style;
               throw new BusinessException(Constants.CAMPOS_OBRIGATORIOS);
            }

         }
      }
      private bool ValidarCampos()
      {
         decimal quantidade = 0;
         decimal.TryParse(txtQuantidade.Text.ToStringReplaceMonetario(), out quantidade);
         if (uscCFOP.Id == null)
            throw new BusinessException("É necessário informar o CFOP do Produto.");
         return true;
      }

      private bool VerificarNCM()
      {
         if (!string.IsNullOrEmpty(produto.NCM))
            return true;
         if (string.IsNullOrEmpty(produto.NCM) && MessageBoxUtils.ExibeMensagemQuestion(string.Format("Produto {0} não possui NCM informado!\nDeseja informar agora?", produto.Descricao)))
         {
            usrConsultaNCM frmConsultaNCM = new usrConsultaNCM(produto.Descricao);
            if (frmConsultaNCM.ShowDialog().Value)
            {
               var ncmSelecionado = frmConsultaNCM.NCM;
               if (MessageBoxUtils.ExibeMensagemQuestion(string.Format("Deseja inserir o NCM {0} para o produto {1}?",
                 ncmSelecionado, produto.Descricao)))
               {
                  new ProdutoBusiness().AtualizarNCM(produto.id, ncmSelecionado);
                  //  tabPrincipal.SelectedItem = tabCabecalho;
                  MessageBoxUtils.ExibeMensagemSucesso("NCM Atualizado com Sucesso!");
                  return true;
               }
               else
                  return false;
            }
            else
               return false;
         }
         else
            return false;
         return true;
      }

      private void CalcularBaseIcms()
      {
         decimal valor = txtValorUnitario.Text.ToDecimalOrNull().GetValueOrDefault() *
                     txtQuantidade.Text.ToDecimalOrNull().GetValueOrDefault() +
                     txtValorFreteProduto.Text.ToDecimalOrNull().GetValueOrDefault() +
                     txtValorSeguro.Text.ToDecimalOrNull().GetValueOrDefault() +
                     txtValorDespesas.Text.ToDecimalOrNull().GetValueOrDefault() -
                     txtValorDescontoProduto.Text.ToDecimalOrNull().GetValueOrDefault();

         //aplicar redução na base de calculo do icms
         decimal porcetagemReducao = txtPorcentagemReducao.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal valorReducao = (valor * porcetagemReducao / 100);
         decimal valorBase = valor - valorReducao;

         txtBaseIcms.Text = valorBase.ToString("N2");

         //aplicar ipi na base de calculo do icms
         if (produto != null)
         {
            var produtoImposto = new ProdutoImpostoBusiness().BuscarPorId(produto.id);
            if (produtoImposto.IPICompoeBaseCalculoICMS)
            {
               if (uscCSTICMS.Id.In(101, 201, 202, 203, 900, 00, 10, 20, 70, 90, 30, 60))
               {
                  decimal valorBaseCalculo = txtBaseIcms.Text.ToDecimalOrNull().GetValueOrDefault();
                  decimal valorAliquotaIPI = txtAliquotaIPI.Text.ToDecimalOrNull().GetValueOrDefault();
                  decimal percentualIpi = valorAliquotaIPI / 100;
                  decimal valorIpi = valorBase * percentualIpi;
                  txtBaseIcms.Text = (valorBase + valorIpi).ToString("N2");
               }
            }
         }
         if (tbiImpostoImportacao.Visibility == Visibility.Visible)
            txtBaseCalculoII.Text = txtBaseIcms.Text.ToStringOrEmpty();

         CalcularICMS();
      }
      private void CalcularBasePis()
      {
         decimal valor = txtValorUnitario.Text.ToDecimalOrNull().GetValueOrDefault() *
               txtQuantidade.Text.ToDecimalOrNull().GetValueOrDefault() +
               txtValorFreteProduto.Text.ToDecimalOrNull().GetValueOrDefault() +
               txtValorSeguro.Text.ToDecimalOrNull().GetValueOrDefault() +
               txtValorDespesas.Text.ToDecimalOrNull().GetValueOrDefault() -
               txtValorDescontoProduto.Text.ToDecimalOrNull().GetValueOrDefault();

         txtBasePIS.Text = valor.ToString("N2");

         CalcularPis();
      }

      private void CalcularBaseCofins()
      {
         decimal valor = txtValorUnitario.Text.ToDecimalOrNull().GetValueOrDefault() *
               txtQuantidade.Text.ToDecimalOrNull().GetValueOrDefault() +
               txtValorFreteProduto.Text.ToDecimalOrNull().GetValueOrDefault() +
               txtValorSeguro.Text.ToDecimalOrNull().GetValueOrDefault() +
               txtValorDespesas.Text.ToDecimalOrNull().GetValueOrDefault() -
               txtValorDescontoProduto.Text.ToDecimalOrNull().GetValueOrDefault();

         txtBaseCOFINS.Text = valor.ToString("N2");

         CalcularCofins();
      }
      private void CalcularBaseIpi()
      {
         decimal valor = txtValorUnitario.Text.ToDecimalOrNull().GetValueOrDefault() *
               txtQuantidade.Text.ToDecimalOrNull().GetValueOrDefault() +
               txtValorFreteProduto.Text.ToDecimalOrNull().GetValueOrDefault() +
               txtValorSeguro.Text.ToDecimalOrNull().GetValueOrDefault() +
               txtValorDespesas.Text.ToDecimalOrNull().GetValueOrDefault() -
               txtValorDescontoProduto.Text.ToDecimalOrNull().GetValueOrDefault();
         txtBaseIPI.Text = valor.ToString("N2");


         CalcularIpi();
      }
      private void CalcularICMS()
      {
         decimal baseCalculo = txtBaseIcms.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal aliquotaIcms = txtAliquota.Text.ToDecimalOrNull().GetValueOrDefault();

         decimal icms = baseCalculo * (aliquotaIcms / 100);
         txtValorICMS.Text = icms.ToString("N2");

         if (uscCSTICMS.Id != null && uscCSTICMS.Id.Value == 51)
            CalcularDiferimento();
      }
      private void CalcularICMSST()
      {
         decimal baseCalculoST = txtBaseICMSST.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal aliquotaIcmsST = txtAliquotaICMSST.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal mva = txtMVA.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal calculoMVA = (baseCalculoST * mva / 100) + baseCalculoST;
         txtBaseICMSST.Text = calculoMVA.ToString("N2");
         decimal valorIcmsSt = calculoMVA * aliquotaIcmsST / 100;
         decimal valorIcms = txtValorICMS.Text.ToDecimalOrNull().GetValueOrDefault();
         txtValorICMSST.Text = (valorIcmsSt - valorIcms).ToString("N2");
      }
      private void CalcularPis()
      {
         decimal baseCalculoPis = txtBasePIS.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal aliquotaPis = txtAliquotaPIS.Text.ToDecimalOrNull().GetValueOrDefault();

         txtValorPIS.Text = (baseCalculoPis * aliquotaPis / 100).ToString("N2");

      }
      private void CalcularCofins()
      {
         decimal baseCalculoCofins = txtBaseCOFINS.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal aliquotaCofins = txtAliquotaCOFINS.Text.ToDecimalOrNull().GetValueOrDefault();

         txtValorCOFINS.Text = (baseCalculoCofins * aliquotaCofins / 100).ToString("N2");

      }
      private void CalcularIpi()
      {
         decimal baseCalculoIpi = txtBaseIPI.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal aliquotaIpi = txtAliquotaIPI.Text.ToDecimalOrNull().GetValueOrDefault();

         txtValorIPI.Text = (baseCalculoIpi * aliquotaIpi / 100).ToString("N2");

         CalcularBaseIcms();
      }
      private void CalcularIpiCompoeBaseIcms()
      {
         if (produto != null)
         {
            var produtoImposto = new ProdutoImpostoBusiness().BuscarPorId(produto.id);
            if (produtoImposto.IPICompoeBaseCalculoICMS)
            {
               decimal valorBase = txtBaseIcms.Text.ToDecimalOrNull().GetValueOrDefault();
               decimal valorAliquotaIPI = txtAliquotaIPI.Text.ToDecimalOrNull().GetValueOrDefault();
               decimal percentualIpi = valorAliquotaIPI / 100;
               decimal valorIpi = valorBase * percentualIpi;
               txtBaseIcms.Text = (valorBase + valorIpi).ToString("N2");
            }
         }
      }
      private void CalcularDiferimento()
      {
         decimal porcentagemDiferimento = txtPorcentagemDiferimento.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal valorImcs = txtValorICMS.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal valorDiferido = (valorImcs * porcentagemDiferimento / 100);
         txtValorIcmsDiferido.Text = valorDiferido.ToString("N2");
         txtValorIcmsAposDiferimento.Text = (valorImcs - valorDiferido).ToString("N2");
      }

      private void CalcularTotais(tb_nfe nfe)
      {
         //irá passar aqui quando salvar a nota
         if (nfe != null)
         {
            var produtoNfe = new NFeProdutoBusiness().BuscarPorIdNota(nfe.id);
            txtTotalICMS.Text = produtoNfe.Sum(x => x.ValorICMS.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtTotalBaseICMS.Text = produtoNfe.Sum(x => x.ValorBaseCalculoICMS.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtTotalBaseST.Text = produtoNfe.Sum(x => x.ValorBaseCalculoICMSST.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtTotalST.Text = produtoNfe.Sum(x => x.ValorICMSST.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtTotalFrete.Text = produtoNfe.Sum(x => x.ValorFrete.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtValorTotalSeguro.Text = produtoNfe.Sum(x => x.ValorSeguro.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtTotalDesconto.Text = produtoNfe.Sum(x => x.ValorDesconto.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtTotalDespesas.Text = produtoNfe.Sum(x => x.ValorOutrasDespesas.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtTotalPIS.Text = produtoNfe.Sum(x => x.ValorPIS.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtTotalIPI.Text = produtoNfe.Sum(x => x.ValorIPI.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtTotalCOFINS.Text = produtoNfe.Sum(x => x.ValorCOFINS.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtTotalProdutos.Text = produtoNfe.Sum(x => x.ValorUnitario * x.Quantidade).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            decimal calculoValorTotal = produtoNfe.Sum(x => x.ValorTotalProduto.GetValueOrDefault());
            txtTotalNotaAbaTotais.Text = calculoValorTotal.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
         }
         if (nfeProdutoObservable.Count > 0)
         {
            //preencher valores na aba pagamento
            txtValorTotalPagamento.Text = nfeProdutoObservable.Sum(x => x.ValorTotalProduto.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            txtValorRestante.Text = nfeProdutoObservable.Sum(x => x.ValorTotalProduto.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
            //preencher campo valor total no radapé
            txtValorTotalNota.Text = nfeProdutoObservable.Sum(x => x.ValorTotalProduto.GetValueOrDefault()).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
         }

      }
      private void CalcularDescontoNosTributos()
      {
         //recalculo todo processo de imposto porque o valor da base de dados altera.
         List<tb_nfe_produto> produtoLista = new List<tb_nfe_produto>();
         foreach (var item in nfeProdutoObservable)
         {
            if (item.idCSOSN.In(101, 201, 202, 203, 900) || item.idCSTICMS.In(00, 10, 20, 51, 70, 90))
            {

               var produtoImposto = new ProdutoImpostoBusiness().BuscarPorId(item.idProduto);
               decimal valorIpi = 0;

               //calcular BASE 
               decimal valor = item.ValorUnitario * item.Quantidade +
               item.ValorFrete.GetValueOrDefault() +
               item.ValorSeguro.GetValueOrDefault() +
               item.ValorOutrasDespesas.GetValueOrDefault() -
               item.ValorDesconto.GetValueOrDefault();

               if (produtoImposto != null && produtoImposto.IPICompoeBaseCalculoICMS)
               {
                  valorIpi = item.ValorIPI.GetValueOrDefault();
                  decimal aliquotaIpi = produtoImposto.AliquotaIPISaida.GetValueOrDefault();
                  valorIpi = valor * aliquotaIpi / 100;
                  valor = valor + valorIpi;
               }
               decimal porcetagemReducao = item.PorcentagemReducao.GetValueOrDefault();
               decimal valorReducao = (valor * porcetagemReducao / 100);
               decimal valorBase = valor - valorReducao;

               item.ValorBaseCalculoICMS = valorBase;
            }
            if (item.idCSTPIS.In(1, 2, 3, 49, 99))
            {
               //calcular PIS
               decimal baseCalculoPis = item.ValorBaseCalculoPIS.GetValueOrDefault() - item.ValorDesconto.GetValueOrDefault();
               decimal aliquotaPis = item.ValorAliquotaPIS.GetValueOrDefault();
               item.ValorPIS = (baseCalculoPis * aliquotaPis / 100);
               item.ValorBaseCalculoPIS = baseCalculoPis;
            }
            if (item.idCSTCOFINS.In(1, 2, 3, 49, 99))
            {
               //calcular COFINS
               decimal baseCalculoCofins = item.ValorBaseCalculoCOFINS.GetValueOrDefault() - item.ValorDesconto.GetValueOrDefault();
               decimal aliquotaCofins = item.ValorAliquotaCOFINS.GetValueOrDefault();
               item.ValorCOFINS = (baseCalculoCofins * aliquotaCofins / 100);
               item.ValorBaseCalculoCOFINS = baseCalculoCofins;
            }
            //calcular IPI

            if (item.idCSTIPI.In(0, 99))
            {
               decimal baseCalculoIpi = item.ValorBaseCalculoIPI.GetValueOrDefault() - item.ValorDesconto.GetValueOrDefault();
               decimal aliquotaIpi = item.ValorAliquotaIPI.GetValueOrDefault();
               item.ValorIPI = (baseCalculoIpi * aliquotaIpi / 100);
               item.ValorBaseCalculoIPI = baseCalculoIpi;
               var produtoImposto = new ProdutoImpostoBusiness().BuscarPorId(item.idProduto);
               if (produtoImposto.IPICompoeBaseCalculoICMS)
               {
                  decimal valorBaseCalculo = item.ValorBaseCalculoICMS.GetValueOrDefault();
                  decimal valorAliquotaIPI = txtAliquotaIPI.Text.ToDecimalOrNull().GetValueOrDefault();
                  decimal percentualIpi = valorAliquotaIPI / 100;
                  decimal valorIpi = valorBaseCalculo * percentualIpi;
                  item.ValorBaseCalculoICMS = (valorBaseCalculo + valorIpi);
               }
               //calcular DIFERIMENTO
               decimal porcentagemDiferimento = item.PorcentagemDiferimento.GetValueOrDefault();
               decimal valorImcs = item.ValorICMS.GetValueOrDefault();
               decimal valorDiferido = (valorImcs * porcentagemDiferimento / 100);
               item.ValorICMS = (valorImcs - valorDiferido);
            }
            //altera o valor total do produto de acordo com o desconto.
            decimal valorBaseSt = (item.ValorBaseCalculoICMS.GetValueOrDefault() * item.MVA.GetValueOrDefault() / 100) + item.ValorBaseCalculoICMS.GetValueOrDefault();
            decimal valorIcms = item.ValorBaseCalculoICMS.GetValueOrDefault() * item.ValorAliquotaICMS.GetValueOrDefault() / 100;
            decimal valorSt = 0;
            if (item.idCSTICMS.In(10, 30, 60, 70, 90))
               valorSt = (valorBaseSt * item.ValorAliquotaST.GetValueOrDefault() / 100) - valorIcms;
            decimal valorProduto = item.ValorUnitario * item.Quantidade - item.ValorDesconto.GetValueOrDefault();
            item.ValorIPI = valorProduto * item.ValorAliquotaIPI / 100;
            decimal valorProdutoSomado = valorProduto + valorSt + item.ValorIPI.GetValueOrDefault();

            decimal valorTotalProduto = valorProdutoSomado;
            item.ValorTotalProduto = valorTotalProduto;
            produtoLista.Add(item);
         }
         nfeProdutoObservable.Clear();
         foreach (var item in produtoLista)
         {
            nfeProdutoObservable.Add(item);
         }
         dgProdutos.ItemsSource = nfeProdutoObservable;
      }
      private void CalcularDesconto()
      {
         //calcular desconto quando o checkBox não considerar desconto para calculo dos tributos estiver marcado.
         List<tb_nfe_produto> produtoLista = new List<tb_nfe_produto>();
         foreach (var item in nfeProdutoObservable)
         {
            if (item.ValorDesconto > 0)
            {
               decimal valorTotalProduto = item.ValorTotalProduto.GetValueOrDefault() - item.ValorDesconto.GetValueOrDefault();
               item.ValorTotalProduto = valorTotalProduto;
            }
            produtoLista.Add(item);
         }

         nfeProdutoObservable.Clear();
         foreach (var item in produtoLista)
         {
            nfeProdutoObservable.Add(item);
         }
         dgProdutos.ItemsSource = nfeProdutoObservable;
      }
      private void CalcularPartilhaICMS(decimal valorBaseCalculo, tb_produto produto, tb_nfe_produto nfeProduto = null)
      {
         decimal diferenca = 0;
         decimal valorTotalICMSInterestadual = 0;
         decimal porcentagemOrigem = 0;
         decimal porcentagemDestino = 0;
         decimal valorPartilhaOrigem = 0;
         decimal valorPartilhaDestino = 0;
         decimal valorFCP = 0;
         if (produto != null)
         {
            //base de calculo
            if (valorBaseCalculo == 0)
            {
               valorBaseCalculo = txtValorUnitario.Text.ToDecimalOrNull().GetValueOrDefault() *
                             txtQuantidade.Text.ToDecimalOrNull().GetValueOrDefault() +
                             txtValorFreteProduto.Text.ToDecimalOrNull().GetValueOrDefault() +
                             txtValorSeguro.Text.ToDecimalOrNull().GetValueOrDefault() +
                             txtValorDespesas.Text.ToDecimalOrNull().GetValueOrDefault() -
                             txtValorDescontoProduto.Text.ToDecimalOrNull().GetValueOrDefault();

               if (txtPorcentagemReducao.Text != string.Empty)
               {
                  decimal porcetagemReducao = txtPorcentagemReducao.Text.ToDecimalOrNull().GetValueOrDefault();
                  decimal valorReducao = (valorBaseCalculo * porcetagemReducao / 100);
                  decimal valorBaseReduzida = valorBaseCalculo - valorReducao;

                  valorBaseCalculo = valorBaseReduzida;
               }
            }
            var produtoImposto = new ProdutoImpostoBusiness().BuscarPorIdProduto(produto.id);
            var cliente = new ClienteBusiness().BuscarPorId(uscClienteDestinatario.Id.GetValueOrDefault());

            if (cliente.TipoInscricao.ToInt32().In((int)EnumTipoInscricao.CPF, (int)EnumTipoInscricao.CNPJ))

               if (produtoImposto.idCSTICMS.In(00, 20, 40, 41) || uscCSTICMS.Id.In(00, 20, 40, 41))
               {
                  var empresa = new EmpresaBusiness().BuscarPorId(Base.Core.Principal.Empresa.id);
                  int cst;
                  if (nfeProduto != null)
                  {
                     if (nfeProduto.idCSOSN != null)
                        cst = nfeProduto.idCSOSN.GetValueOrDefault();
                     else
                        cst = nfeProduto.idCSTICMS.GetValueOrDefault();
                  }
                  else
                  {
                     cst = uscCSTICMS.Id.GetValueOrDefault();
                  }

                  if (chkPartilha.IsChecked == true)
                  {
                     partilhaICMS = new PartilhaICMSBusiness().BuscarPorAno(DateTime.Now.Year);
                     decimal aliquotaFCP = 0;
                     if (produtoImposto.PorcentagemFCP != null)
                        aliquotaFCP = produtoImposto.PorcentagemFCP.GetValueOrDefault();

                     if (produtoSelecionado != null && produtoSelecionado.PorcentagemFCP.HasValue)
                        aliquotaFCP = produtoSelecionado.PorcentagemFCP.GetValueOrDefault();
                     valorFCP = valorBaseCalculo * (aliquotaFCP / 100);
                     produto.tb_produto_icms_interestadual = new ProdutoICMSInterestadualBusiness().BuscarPorIdProduto(produto.id);

                     if (produto.CalcularFCP)
                     {
                        txtPorcentagemFCP.Text = aliquotaFCP.ToString("F2");
                        txtValorFCP.Text = valorFCP.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
                     }

                     if (produto.UtilizarTabelaICMS && produto.idTabelaICMS.HasValue)
                     {
                        if (uscClienteDestinatario.Id.HasValue)
                        {
                           var tabelaIcms = new TabelaICMSInterestadualBusiness().BuscarPorId(produto.idTabelaICMS.Value);
                           this.aliquotaIntraestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoOrigem == cliente.tb_cidade.idEstado && x.idEstadoDestino == cliente.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
                           this.aliquotaInterestadual = new ICMSInterestadualBusiness().BuscarPorFiltro("idTabela", produto.idTabelaICMS.Value).Where(x => x.idEstadoDestino == cliente.tb_cidade.idEstado && x.idEstadoOrigem == empresa.tb_cidade.idEstado).FirstOrDefault().PorcentagemAliquota;
                           diferenca = (Math.Abs(aliquotaIntraestadual.Value - this.aliquotaInterestadual.Value));
                           valorTotalICMSInterestadual = valorBaseCalculo * (diferenca / 100);
                           porcentagemOrigem = partilhaICMS.PorcentagemOrigem;
                           porcentagemDestino = partilhaICMS.PorcentagemDestino;
                           valorPartilhaOrigem = valorTotalICMSInterestadual * (porcentagemOrigem / 100);
                           valorPartilhaDestino = valorTotalICMSInterestadual * (porcentagemDestino / 100);

                           decimal valorIPI = 0;
                           if (produtoImposto.IPICompoeBaseCalculoICMS)
                              valorIPI = txtValorIPI.Text.ToDecimal();

                           var valorbasepartilha = (valorBaseCalculo.ToString().ToDecimal() + valorIPI);//pega a base da partilhaicms e soma com o valor do ipi

                           txtBasePartilhaICMS.Text = valorbasepartilha.ToString();
                           txtPorcentagemDestino.Text = porcentagemDestino.ToString("F2");
                           txtValorDestino.Text = valorPartilhaDestino.ToString("F2");

                           //Se for do simples nacional calcula somente o destino
                           if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional)
                           {
                              txtPorcentagemOrigem.Text = porcentagemOrigem.ToString("F2");
                              txtValorICMSOrigem.Text = valorPartilhaOrigem.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
                           }
                           else
                           {
                              txtPorcentagemOrigem.Text = 0m.ToString();
                              txtValorICMSOrigem.Text = 0m.ToString();
                           }
                           //txtValorTotalICMSInterestadual.Text = (valorTotalICMSInterestadual).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);

                           if (produto.CalcularFCP)
                           {
                              txtPorcentagemFCP.Text = aliquotaFCP.ToString("F2");
                              txtValorFCP.Text = valorFCP.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
                           }
                        }
                     }
                     else if (produto.tb_produto_icms_interestadual != null && produto.tb_produto_icms_interestadual.Count > 0 && produto.tb_produto_icms_interestadual.Where(x => x.idEstado == cliente.tb_cidade.idEstado).Any())
                     {
                        if (uscClienteDestinatario.Id.HasValue)
                        {
                           var icmsInterstadual = produto.tb_produto_icms_interestadual.Where(x => x.idEstado == cliente.tb_cidade.idEstado).FirstOrDefault();
                           if (produto.tb_produto_imposto == null)
                              produto = produtoBusiness.BuscarPorId(produto.id);
                           diferenca = (Math.Abs(produto.tb_produto_imposto.AliquotaICMS.GetValueOrDefault() - icmsInterstadual.Aliquota.GetValueOrDefault()));
                           valorTotalICMSInterestadual = valorBaseCalculo * (diferenca / 100);
                           porcentagemOrigem = partilhaICMS.PorcentagemOrigem;
                           porcentagemDestino = partilhaICMS.PorcentagemDestino;
                           valorPartilhaOrigem = valorTotalICMSInterestadual * (porcentagemOrigem / 100);
                           valorPartilhaDestino = valorTotalICMSInterestadual * (porcentagemDestino / 100);

                           txtBasePartilhaICMS.Text = valorBaseCalculo.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
                           txtPorcentagemDestino.Text = porcentagemDestino.ToString("F2");
                           txtValorDestino.Text = valorPartilhaDestino.ToString("F2");

                           //Se for do simples nacional calcula somente o destino
                           if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional)
                           {
                              txtPorcentagemOrigem.Text = porcentagemOrigem.ToString("F2");
                              txtValorICMSOrigem.Text = valorPartilhaOrigem.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
                           }
                           else
                           {
                              txtPorcentagemOrigem.Text = 0m.ToString();
                              txtValorICMSOrigem.Text = 0m.ToString();
                           }
                           //txtValorTotalICMSInterestadual.Text = (valorTotalICMSInterestadual).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
                        }
                     }
                     else
                     {
                        throw new BusinessException("Não foi possível calcular o ICMS Interestadual, pois a tabela interestadual de ICMS não foi vinculada ao produto. Para corrigir a situação, acesse o menu Arquivos, Produtos, e, vá até a aba Impostos e, sub-aba LC 87/2015 e, vincule a tabela do produto.");
                     }
                     if (nfeProduto != null)
                     {
                        nfeProduto.idPartilhaICMS = partilhaICMS.id;
                        nfeProduto.PorcentagemFCP = aliquotaFCP;
                        nfeProduto.ValorFCP = valorFCP;
                        nfeProduto.PorcentagemInterestadual = this.aliquotaInterestadual;

                        if (nfeProduto.ValorAliquotaICMS.GetValueOrDefault() > 0)
                           nfeProduto.PorcentagemIntraestadual = nfeProduto.ValorAliquotaICMS;
                        else if (nfeProduto.ValorAliquotaST.GetValueOrDefault() > 0)
                           nfeProduto.PorcentagemIntraestadual = nfeProduto.ValorAliquotaST.GetValueOrDefault();
                        else
                           nfeProduto.PorcentagemIntraestadual = this.aliquotaIntraestadual;

                        nfeProduto.ValorInterestadual = valorPartilhaOrigem;
                        nfeProduto.PorcentagemOrigemPartilha = porcentagemOrigem;
                        nfeProduto.ValorIntraestadual = valorPartilhaDestino;
                        nfeProduto.PorcentagemDestinoPartilha = porcentagemDestino;
                     }

                  }
                  else
                     tbiPartilhaICMS.Visibility = Visibility.Collapsed;
               }

         }
      }
      private void txtBaseIcms_TextChanged(object sender, TextChangedEventArgs e)
      {
         CalcularICMS();

         if (uscCSTICMS.Id.In(70, 90, 10, 201, 202, 203, 900))
         {
            //recalcular a base do icmsST quando alterar o valor da base manualmente.
            decimal baseCalculoST = txtBaseIcms.Text.ToDecimalOrNull().GetValueOrDefault();
            decimal aliquotaIcmsST = txtAliquotaICMSST.Text.ToDecimalOrNull().GetValueOrDefault();
            decimal mva = txtMVA.Text.ToDecimalOrNull().GetValueOrDefault();
            decimal calculoMVA = (baseCalculoST * mva / 100) + baseCalculoST;
            txtBaseICMSST.Text = calculoMVA.ToString("N2");
            decimal valorIcmsSt = calculoMVA * aliquotaIcmsST / 100;
            decimal valorIcms = txtValorICMS.Text.ToDecimalOrNull().GetValueOrDefault();
            txtValorICMSST.Text = (valorIcmsSt - valorIcms).ToString("N2");
         }
      }

      private void txtAliquota_TextChanged(object sender, TextChangedEventArgs e)
      {
         CalcularICMS();
      }

      private void txtMVA_TextChanged(object sender, TextChangedEventArgs e)
      {
         //recalcular valores do icms st ao digitar MVA manualmente
         decimal baseCalculoST = txtBaseIcms.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal aliquotaIcmsST = txtAliquotaICMSST.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal mva = txtMVA.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal calculoMVA = (baseCalculoST * mva / 100) + baseCalculoST;
         txtBaseICMSST.Text = calculoMVA.ToString("N2");
         decimal valorIcmsSt = calculoMVA * aliquotaIcmsST / 100;
         decimal valorIcms = txtValorICMS.Text.ToDecimalOrNull().GetValueOrDefault();
         txtValorICMSST.Text = (valorIcmsSt - valorIcms).ToString("N2");
      }

      private void txtAliquotaICMSST_TextChanged(object sender, TextChangedEventArgs e)
      {
         //recalcular valores do icms st ao digitar MVA manualmente
         decimal baseCalculoST = txtBaseIcms.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal aliquotaIcmsST = txtAliquotaICMSST.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal mva = txtMVA.Text.ToDecimalOrNull().GetValueOrDefault();
         decimal calculoMVA = (baseCalculoST * mva / 100) + baseCalculoST;
         txtBaseICMSST.Text = calculoMVA.ToString("N2");
         decimal valorIcmsSt = calculoMVA * aliquotaIcmsST / 100;
         decimal valorIcms = txtValorICMS.Text.ToDecimalOrNull().GetValueOrDefault();
         txtValorICMSST.Text = (valorIcmsSt - valorIcms).ToString("N2");
      }
      private void txtPorcentagemReducao_LostFocus(object sender, RoutedEventArgs e)
      {
         //aplicar redução na base

         if (rdbValorBase.IsChecked == true)
         {
            if (MessageBoxUtils.ExibeMensagemQuestion("Deseja reduzir a Base de Calculo do ICMS?"))
            {
               decimal percentualReducao = txtPorcentagemReducao.Text.ToDecimalOrNull().GetValueOrDefault();
               if (percentualReducao > 0)
               {
                  decimal valorBase = txtBaseIcms.Text.ToDecimalOrNull().GetValueOrDefault();
                  decimal valorReducao = (valorBase * percentualReducao / 100);
                  txtBaseIcms.Text = (valorBase - valorReducao).ToString("N2");
               }
            }
         }
         else if (rdbValorOriginal.IsChecked == true)
            CalcularBaseIcms();

         txtPorcentagemST.Text = txtPorcentagemReducao.Text.ToDecimalOrNull().ToStringOrNull("N2");

         CalcularPartilhaICMS(0, produto);
      }

      private void txtBasePIS_TextChanged(object sender, TextChangedEventArgs e)
      {
         CalcularPis();
      }

      private void txtAliquotaPIS_TextChanged(object sender, TextChangedEventArgs e)
      {
         CalcularPis();
      }

      private void txtBaseCOFINS_TextChanged(object sender, TextChangedEventArgs e)
      {
         CalcularCofins();
      }

      private void txtAliquotaCOFINS_TextChanged(object sender, TextChangedEventArgs e)
      {
         CalcularCofins();
      }

      private void txtBaseIPI_TextChanged(object sender, TextChangedEventArgs e)
      {
         CalcularIpi();
      }

      private void txtAliquotaIPI_TextChanged(object sender, TextChangedEventArgs e)
      {
         CalcularIpi();
      }
      private void uscCSTICMS_EventoCodigoAlterado(object sender, CodigoAlteradoArgs e)
      {
         if (uscCSTICMS.Id != null && uscCSTICMS.Id.Value == 51)
            txtPorcentagemDiferimento.IsEnabled = true;
         else
         {
            txtPorcentagemDiferimento.IsEnabled = false;
            txtPorcentagemDiferimento.Text = string.Empty;
            txtValorIcmsAposDiferimento.Text = string.Empty;
            txtValorIcmsDiferido.Text = string.Empty;
         }
         if (uscCSTICMS.Id != null && cliente != null && cliente.ConsumidorFinal && uscCSTICMS.Id.In(10, 30, 50, 51, 70, 90, 101, 201, 202, 203, 900))
         {
            uscCSTICMS.Id = null;
            MessageBoxUtils.ExibeMensagemAdvertencia("CST/CSOSN não é compatível com o cliente consumidor final!");
            uscCSTICMS.Focus();
         }
         if (uscCSTICMS.Id != null && uscCSTICMS.Id.In(10, 30, 70, 90, 201, 202, 203, 900))
            tbiICMSST.Visibility = Visibility.Visible;
         else
            tbiICMSST.Visibility = Visibility.Collapsed;
         if (uscCSTICMS.Id.In(101, 102, 201, 900, 0, 10, 20, 51, 70, 90))
         {
            txtAliquota.IsEnabled = true;
            CalcularBaseIcms();
         }
         else
         {
            txtAliquota.IsEnabled = false;
            txtAliquota.Text = string.Empty;
         }
         if (uscCSTICMS.Id.In(20, 51, 70, 90))
            txtPorcentagemReducao.IsEnabled = true;
         else
         {
            txtPorcentagemReducao.IsEnabled = false;
            txtPorcentagemReducao.Text = string.Empty;
            txtPorcentagemST.Text = string.Empty;
         }
         if (chkPartilha.IsChecked==true)
            tbiPartilhaICMS.Visibility = Visibility.Visible;
         else
            tbiPartilhaICMS.Visibility = Visibility.Collapsed;

         if (uscCSTICMS.Id.In(60, 500) && cliente.ConsumidorFinal == false)
            tbiIcmsAntecipado.Visibility = Visibility.Visible;
         else
            tbiIcmsAntecipado.Visibility = Visibility.Collapsed;

         CalcularPartilhaICMS(0, produto);
      }

      private void txtPorcentagemDiferimento_SelectionChanged(object sender, RoutedEventArgs e)
      {
         CalcularDiferimento();
      }

      private void dgProdutos_PreviewKeyDown(object sender, KeyEventArgs e)
      {
         if (dgProdutos.SelectedItem != null && nfe != null && (nfe.idNFStatus != (int)EnumStatusNFe.Cancelada && nfe.idNFStatus != (int)EnumStatusNFe.EmitidaComSucesso && nfe.idNFStatus != (int)EnumStatusNFe.Inutilizada && nfe.idNFStatus != (int)EnumStatusNFe.CanceladaSemEmissao))
         {
            var nfeProduto = dgProdutos.SelectedItem as tb_nfe_produto;
            var pagamento = new NFeFormaPagamentoBusiness().BuscarPorIdNFe(nfe.id);

            if (nfeProduto != null)
            {
               if (e.Key == Key.Delete)
               {
                  if (MessageBoxUtils.ExibeMensagemQuestionExcluirRegistros())
                  {
                     formaPagamentoObservable.Clear();
                     foreach (var item in pagamento)
                     {
                        formaPagamentoObservable.Remove(item);
                     }
                     nfeProdutoObservable.Remove(nfeProduto);
                     if (nfeProdutoObservable.Count == 0)
                        tbiPagamento.IsEnabled = false;

                     dgProdutos.ItemsSource = null;
                     dgProdutos.ItemsSource = nfeProdutoObservable;
                     btnAdicionarProduto.Content = "Adicionar";
                     btnAdicionarProduto.Tag = "Adicionar";

                     LimparCamposProduto();
                     uscProduto.IsEnabled = true;
                     uscProduto.Focus();

                     if (nfeProdutoObservable.Count == 0)
                        txtNaturezaOperacao.Text = "";
                  }
                  else { e.Handled = true; };
               }
            }
         }
      }

      private void dgProdutos_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         btnExcluirProduto.Visibility = Visibility.Collapsed;
         btnCalcularDescontoTotal.IsEnabled = false;
         tbiTotais.IsEnabled = false;

         if (dgProdutos.SelectedItem != null)
         {
            produtoSelecionado = null;
            produtoSelecionado = (dgProdutos.SelectedItem as tb_nfe_produto);
            PreencherProduto(produtoSelecionado);
            uscProduto.IsEnabled = false;
            btnAdicionarProduto.Content = "Salvar";
            btnAdicionarProduto.Tag = "Salvar";
            if (dgProdutos.SelectedItem != null && nfe != null && (nfe.idNFStatus != (int)EnumStatusNFe.Cancelada && nfe.idNFStatus != (int)EnumStatusNFe.EmitidaComSucesso && nfe.idNFStatus != (int)EnumStatusNFe.Inutilizada && nfe.idNFStatus != (int)EnumStatusNFe.CanceladaSemEmissao))
            {
               btnExcluirProduto.Visibility = Visibility.Visible;
               btnAdicionarProduto.IsEnabled = true;
            }

            if (produtoSelecionado.idCSTICMS.HasValue && (produtoSelecionado.idCSTICMS.Value == 40 || produtoSelecionado.idCSTICMS.Value == 41 || produtoSelecionado.idCSTICMS.Value == 50))
            {
               txtBaseIcms.IsEnabled = false;
               txtBaseIcms.Text = string.Empty;
            }
            else if (produtoSelecionado.idCSOSN.HasValue && (produtoSelecionado.idCSOSN.Value == 102 || produtoSelecionado.idCSOSN.Value == 103
              || produtoSelecionado.idCSOSN.Value == 300 || produtoSelecionado.idCSOSN.Value == 400
              || produtoSelecionado.idCSOSN.Value == 500))
            {
               txtBaseIcms.IsEnabled = false;
               txtBaseIcms.Text = string.Empty;
            }
            else
            {
               txtBaseIcms.IsEnabled = true;
               txtBaseIcms.Text = produtoSelecionado.ValorBaseCalculoICMS.ToStringOrEmpty("N2");
            }

         }
         else
         {
            uscProduto.IsEnabled = true;
         }
         //preencher campo icms
         if (produtoSelecionado.idCSTICMS != 51)
         {
            txtValorIcmsDiferido.Text = string.Empty;
            txtValorIcmsAposDiferimento.Text = string.Empty;
         }
         if (produtoSelecionado.idCSTICMS == 41)
            txtBaseIcms.Text = string.Empty;
         if (produtoSelecionado.idCSTICMS.In(60, 500))
         {
            txtBaseIcmsStAnt.Text = produtoSelecionado.ValorBaseCalculoICMSSTRet.ToStringOrNull("N2");
            txtPercentualIcmsStAnt.Text = produtoSelecionado.ValorAliquotaSTRet.ToStringOrNull("N2");
            txtValorIcmsAnt.Text = produtoSelecionado.ValorICMSSubstituto.ToStringOrNull("N2");
            txtValorICMSSTAnt.Text = produtoSelecionado.ValorICMSSTRetido.ToStringOrNull("N2");
            txtpercentualFCPAnt.Text = produtoSelecionado.PorcentagemFCPRet.ToStringOrNull("N2");
         }

         txtPorcentagemReducao.Text = produtoSelecionado.PorcentagemReducao.ToString();
         txtPorcentagemST.Text = produtoSelecionado.PorcentagemReducao.ToString();
         txtBasePartilhaICMS.Text = produtoSelecionado.ValorBaseCalculoPartilha.ToString();

         if (produtoSelecionado.idCSTICMS == 30 || produtoSelecionado.idCSTICMS == 70 || produtoSelecionado.idCSTICMS == 90 || produtoSelecionado.idCSTICMS == 10 ||
             produtoSelecionado.idCSTICMSCSOSN == 201 || produtoSelecionado.idCSTICMSCSOSN == 202 || produtoSelecionado.idCSTICMSCSOSN == 203 || produtoSelecionado.idCSTICMSCSOSN == 900)
         {
            tbiICMSST.Visibility = Visibility.Visible;
            txtMVA.Text = produtoSelecionado.MVA.ToString();
         }
         //preencher pis/cofins do produto selecionado no grid
         if (produtoSelecionado.ValorPIS != null)
         {
            txtBasePIS.Text = produtoSelecionado.ValorBaseCalculoPIS.ToStringOrNull("N2");
            txtAliquotaPIS.Text = produtoSelecionado.ValorAliquotaPIS.ToStringOrNull("N2");
            txtValorPIS.Text = produtoSelecionado.ValorPIS.ToStringOrNull("N2");
         }
         if (produtoSelecionado.ValorCOFINS != null)
         {
            txtBaseCOFINS.Text = produtoSelecionado.ValorBaseCalculoCOFINS.ToStringOrNull("N2");
            txtAliquotaCOFINS.Text = produtoSelecionado.ValorAliquotaCOFINS.ToStringOrNull("N2");
            txtValorCOFINS.Text = produtoSelecionado.ValorCOFINS.ToStringOrNull("N2");
         }

      }
      private void PreencherProduto(tb_nfe_produto produtoSelecionado)
      {
         //Groupbox Dados Produto
         if (produtoSelecionado != null)
         {
            //Geral
            uscProduto.Id = produtoSelecionado.idProduto;
            if (produtoSelecionado.tb_produto != null)
               if (empresa.tb_cidade.idEstado == estadoCliente.id)
                  uscCFOP.Id = produtoSelecionado.tb_produto.idCFOPEstadual;
               else if (empresa.tb_cidade.idEstado != estadoCliente.id && empresa.tb_cidade.tb_estado.idPais == estadoCliente.idPais)
                  uscCFOP.Id = produtoSelecionado.tb_produto.idCFOPInterestadual;
               else
                  uscCFOP.Id = produtoSelecionado.tb_produto.idCFOPExterior;

            txtQuantidade.Text = produtoSelecionado.Quantidade.ToString();
            txtValorUnitario.Text = produtoSelecionado.ValorUnitario.ToString("N2");

            if (produtoSelecionado.idCSTICMS.HasValue && (produtoSelecionado.idCSTICMS.Value.In(30, 40, 41, 50, 60)))// == 30 || produtoSelecionado.idCSTICMS.Value == 40 || produtoSelecionado.idCSTICMS.Value == 41 || produtoSelecionado.idCSTICMS.Value == 50 || produtoSelecionado.idCSTICMS.Value == 60))
               txtBaseIcms.Text = string.Empty;
            else if (produtoSelecionado.idCSOSN.HasValue && (produtoSelecionado.idCSOSN.Value.In(102, 103, 300, 400, 500)))
               txtBaseIcms.Text = string.Empty;

            txtValorDescontoProduto.Text = produtoSelecionado.ValorDesconto.ToStringOrNull("N2");
            txtValorFreteProduto.Text = produtoSelecionado.ValorFrete.ToStringOrNull("N2");
            txtValorSeguro.Text = produtoSelecionado.ValorSeguro.ToStringOrNull("N2");
            txtValorDespesas.Text = produtoSelecionado.ValorOutrasDespesas.ToStringOrNull("N2");
            txtComplementoDescricao.Text = produtoSelecionado.ComplementoDescricao.ToStringOrEmpty();
            txtPedido.Text = produtoSelecionado.Pedido.ToStringOrEmpty();
            txtcodigopedido.Text = produtoSelecionado.CodPedido.ToStringOrNull();
            txtPercentualIPIDevolvido.Text = produtoSelecionado.PercentualIPIDevolv.ToStringOrNull("N2");
            txtIPIDevolvido.Text = produtoSelecionado.ValorIPIDevolv.ToStringOrNull("N2");

            //ICMS
            if (uscCSTICMS.Tag == "CST")
            {
               //COFINS
               uscCSTCOFINS.Id = produtoSelecionado.idCSTCOFINS;

               uscCSTICMS.Id = produtoSelecionado.idCSTICMS.ToIntOrNull();

               if (parametro.Industrial || NotaImportacao())
               {
                  //IPI
                  uscCSTIPI.Id = produtoSelecionado.idCSTIPI;
                  if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional || !cliente.ConsumidorFinal)
                  {
                     txtBaseIPI.Text = produtoSelecionado.ValorBaseCalculoIPI.ToStringOrEmpty("N2");
                     txtAliquotaIPI.Text = produtoSelecionado.ValorAliquotaIPI.ToStringOrEmpty("N2");
                     txtValorIPI.Text = produtoSelecionado.ValorIPI.ToStringOrEmpty("N2");
                  }
               }

               if (uscCSTICMS.Id != null && uscCSTICMS.Id.Value == 51)
               {
                  txtPorcentagemDiferimento.Text = produtoSelecionado.PorcentagemDiferimento.ToString();
                  txtPorcentagemReducao.Text = produtoSelecionado.PorcentagemReducao.ToString();
               }

               //PIS
               uscCSTPIS.Id = produtoSelecionado.idCSTPIS;
               if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional || !cliente.ConsumidorFinal)
               {
                  txtBasePIS.Text = produtoSelecionado.ValorBaseCalculoPIS.ToStringOrEmpty("N2");
                  txtAliquotaPIS.Text = produtoSelecionado.ValorAliquotaPIS.ToStringOrEmpty("N2");
                  txtValorPIS.Text = produtoSelecionado.ValorPIS.ToStringOrEmpty("N2");
               }

               if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional || !cliente.ConsumidorFinal)
               {
                  txtBaseCOFINS.Text = produtoSelecionado.ValorBaseCalculoCOFINS.ToStringOrEmpty("N2");
                  txtAliquotaCOFINS.Text = produtoSelecionado.ValorAliquotaCOFINS.ToStringOrEmpty("N2");
                  txtValorCOFINS.Text = produtoSelecionado.ValorCOFINS.ToStringOrEmpty("N2");
               }

            }
            else
            {
               uscCSTICMS.Id = produtoSelecionado.idCSOSN.ToIntOrNull();
               if (NotaImportacao())
               {
                  //IPI
                  uscCSTIPI.Id = produtoSelecionado.idCSTIPI;
                  if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional || !cliente.ConsumidorFinal)
                  {
                     txtBaseIPI.Text = produtoSelecionado.ValorBaseCalculoIPI.ToStringOrEmpty("N2");
                     txtAliquotaIPI.Text = produtoSelecionado.ValorAliquotaIPI.ToStringOrEmpty("N2");
                     txtValorIPI.Text = produtoSelecionado.ValorIPI.ToStringOrEmpty("N2");
                  }
               }
            }

            uscOrigem.Id = produtoSelecionado.idOrigemMercadoria.ToIntOrNull();
            txtAliquota.Text = produtoSelecionado.ValorAliquotaICMS.ToString();
            if (!cliente.ConsumidorFinal)
            {

               txtBaseIcms.Text = produtoSelecionado.ValorBaseCalculoICMS.ToStringOrEmpty("N2");


               if (produtoSelecionado.ValorAliquotaST.GetValueOrDefault() > 0)
                  txtAliquotaICMSST.Text = produtoSelecionado.ValorAliquotaST.ToString();
               if (uscCSTICMS.Tag == "CST")
               {
                  txtAliquota.Text = produtoSelecionado.ValorAliquotaICMS.ToStringOrEmpty("N2");
                  txtValorICMS.Text = produtoSelecionado.ValorICMS.ToStringOrEmpty("N2");

               }
               if (uscCSTICMS.Tag == "CSOSN")
                  txtPorcentagemST.Text = produtoSelecionado.PorcentagemReducaoST.GetValueOrDefault().ToString();

            }
         }
         //ICMS ST
         if (tbiICMSST.Visibility == Visibility.Visible)
         {
            if (!cliente.ConsumidorFinal)
            {
               txtBaseICMSST.Text = produtoSelecionado.ValorBaseCalculoICMSST.ToString();
               txtValorICMSST.Text = produtoSelecionado.ValorICMSST.ToString();
            }
            if (produtoSelecionado.ValorAliquotaST.GetValueOrDefault() > 0)
            {
               txtMVA.Text = produtoSelecionado.MVA.ToStringOrEmpty("N2");
               txtPorcentagemST.Text = produtoSelecionado.PorcentagemReducaoST.ToString();
               txtAliquotaICMSST.Text = produtoSelecionado.ValorAliquotaST.ToStringOrEmpty("N2");
            }
            else
            {
               txtMVA.Text = string.Empty;
               txtPorcentagemST.Text = string.Empty;
               txtAliquotaICMSST.Text = string.Empty;
               txtValorICMSST.Text = string.Empty;
            }
         }

         txtQuantidade.Text = produtoSelecionado.Quantidade.ToString();
         uscCFOP.Id = produtoSelecionado.idCFOP;

         //Partilha ICMS
         if (produtoSelecionado.ValorBaseCalculoPartilha.HasValue)
            txtBasePartilhaICMS.Text = produtoSelecionado.ValorBaseCalculoPartilha.ToStringOrNull();
         if (produtoSelecionado.PorcentagemFCP.HasValue)
            txtPorcentagemFCP.Text = produtoSelecionado.PorcentagemFCP.ToStringOrNull();
         if (produtoSelecionado.ValorFCP.HasValue)
            txtValorFCP.Text = produtoSelecionado.ValorFCP.ToStringOrNull();
         if (produtoSelecionado.PorcentagemOrigemPartilha.HasValue)
            txtPorcentagemOrigem.Text = produtoSelecionado.PorcentagemOrigemPartilha.ToStringOrNull();
         if (produtoSelecionado.ValorInterestadual.HasValue)
            txtValorICMSOrigem.Text = produtoSelecionado.ValorInterestadual.ToStringOrNull();
         if (produtoSelecionado.PorcentagemDestinoPartilha.HasValue)
            txtPorcentagemDestino.Text = produtoSelecionado.PorcentagemDestinoPartilha.ToStringOrNull();
         if (produtoSelecionado.ValorIntraestadual.HasValue)
            txtValorDestino.Text = produtoSelecionado.ValorIntraestadual.ToStringOrNull();
         if (produtoSelecionado.idPartilhaICMS != null)
         {
            chkPartilha.IsChecked = true;
            tbiPartilhaICMS.Visibility = Visibility.Visible;
         }
         PreencherCamposDeclaracaoImportacao(produtoSelecionado);
      }

      private bool NotaImportacao()
      {
         if (cboTipoNota.SelectedValue.ToInt32() == (int)EnumTipoNota.Entrada && uscClienteDestinatario.Id.HasValue)
         {
            var cliente = new ClienteBusiness().BuscarPorId(uscClienteDestinatario.Id.Value);
            if (cliente != null && cliente.tb_cidade.tb_estado.idPais != Constants.ID_PAIS_BRASIL)
               return true;
            else
               return false;
         }
         return false;
      }
      private void ChkPartilha_Click(object sender, RoutedEventArgs e)
      {
         if (chkPartilha.IsChecked == true)
         {
            tbiPartilhaICMS.Visibility = Visibility.Visible;
            CalcularPartilhaICMS(0, produto);
         }
         else
         {
            tbiPartilhaICMS.Visibility = Visibility.Collapsed;
            txtBasePartilhaICMS.Text = string.Empty;
            txtPorcentagemFCP.Text = string.Empty;
            txtValorFCP.Text = string.Empty;
            txtPorcentagemOrigem.Text = string.Empty;
            txtValorICMSOrigem.Text = string.Empty;
            txtPorcentagemDestino.Text = string.Empty;
            txtValorDestino.Text = string.Empty;
         }
      }

      private void btnExcluirProduto_Click(object sender, RoutedEventArgs e)
      {
         if (dgProdutos.SelectedItem != null && nfe != null && (nfe.idNFStatus != (int)EnumStatusNFe.Cancelada && nfe.idNFStatus != (int)EnumStatusNFe.EmitidaComSucesso && nfe.idNFStatus != (int)EnumStatusNFe.Inutilizada && nfe.idNFStatus != (int)EnumStatusNFe.CanceladaSemEmissao))
         {
            var nfeProduto = dgProdutos.SelectedItem as tb_nfe_produto;
            var pagamento = new NFeFormaPagamentoBusiness().BuscarPorIdNFe(nfe.id);
            var parcelas = new NFePagamentoBusiness().BuscarPorIdNFe(nfe.id);
            if (nfeProduto != null && venda == null)
            {
               if (MessageBoxUtils.ExibeMensagemQuestionAlterarRegistros())
               {
                  formaPagamentoObservable.Clear();
                  foreach (var item in pagamento)
                  {
                     formaPagamentoObservable.Remove(item);
                  }
                  pagamentoObservable.Clear();
                  foreach (var item in parcelas)
                  {
                     pagamentoObservable.Remove(item);
                  }
                  nfeProdutoObservable.Remove(nfeProduto);
                  if (nfeProdutoObservable.Count == 0)
                     tbiPagamento.IsEnabled = false;

                  dgProdutos.ItemsSource = null;
                  dgProdutos.ItemsSource = nfeProdutoObservable;

                  btnAdicionarProduto.Content = "Adicionar";
                  btnAdicionarProduto.Tag = "Adicionar";
                  if (venda != null)
                     btnAdicionarProduto.IsEnabled = false;

                  LimparCamposProduto();
                  uscProduto.IsEnabled = true;

                  if (nfeProdutoObservable.Count == 0)
                     txtNaturezaOperacao.Text = "";
                  CalcularTotais(null);
               }
               else { e.Handled = true; };
            }
         }
      }

      private void txtPesquisaPorDescricao_TextChanged(object sender, TextChangedEventArgs e)
      {
         if (!string.IsNullOrEmpty((sender as TextBox).Text) && nfeProdutoObservable.ToList().Count > 0)
         {
            var listaPesquisados = nfeProdutoObservable.Where(x => x.tb_produto.Descricao.ToLower().Contains((sender as TextBox).Text.ToLower())).ToList();
            dgProdutos.ItemsSource = null;
            dgProdutos.ItemsSource = listaPesquisados;
         }
         else
         {
            dgProdutos.ItemsSource = null;
            dgProdutos.ItemsSource = nfeProdutoObservable;
         }
      }
      private void uscCFOPMassa_EventoCodigoAlterado(object sender, CodigoAlteradoArgs e)
      {
         if (((sender as usrConsultaControle).Id != 0) && nfeProdutoObservable.ToList().Count > 0)
         {
            var listaPesquisados = nfeProdutoObservable.Where(x => x.idCFOP == (sender as usrConsultaControle).Id).ToList();
            dgProdutos.ItemsSource = null;
            dgProdutos.ItemsSource = listaPesquisados;
         }
         else
         {
            dgProdutos.ItemsSource = null;
            dgProdutos.ItemsSource = nfeProdutoObservable;
         }
      }
      private void btnCalcularDescontoTotal_Click(object sender, RoutedEventArgs e)
      {
         if (nfeProdutoObservable != null && nfeProdutoObservable.Count > 0 && txtDescontoTotal.IsReadOnly == false)
         {
            if (string.IsNullOrEmpty(txtDescontoTotal.Text) || txtDescontoTotal.Text == "R$ 0,00"
              && MessageBoxUtils.ExibeMensagemQuestion("Deseja retirar o valor do desconto dos produtos?\nTodos os impostos serão recalculados!"))
            {
               //retirar desconto aplicado e restabelecer valor original.
               RatearDesconto(txtDescontoTotal.Text.ToDecimalOrNull().GetValueOrDefault());

               foreach (var item in nfeProdutoObservable)
               {
                  decimal valorTotalProduto = item.ValorUnitario * item.Quantidade;
                  item.ValorTotalProduto = valorTotalProduto;
                  item.ValorBaseCalculoPIS = valorTotalProduto;
                  item.ValorBaseCalculoCOFINS = valorTotalProduto;
                  item.ValorBaseCalculoIPI = valorTotalProduto;
               }
               CalcularDescontoNosTributos();
            }
            else if (!string.IsNullOrEmpty(txtDescontoTotal.Text) && chkNaoConsiderarDescontoTributos.IsChecked.Value == true &&
                MessageBoxUtils.ExibeMensagemQuestion("Deseja ratear o desconto para todos os produtos?"))
            {
               //desconto aplicado apenas ao valor total da nota
               RatearDesconto(txtDescontoTotal.Text.ToDecimalOrNull().GetValueOrDefault());
               CalcularDesconto();
            }
            else if (!string.IsNullOrEmpty(txtDescontoTotal.Text) && chkNaoConsiderarDescontoTributos.IsChecked.Value == false &&
              MessageBoxUtils.ExibeMensagemQuestion("Deseja ratear o desconto para todos os produtos?\nTodos os impostos serão recalculados!"))
            {
               //desconto aplicado nos valores dos tributos.
               RatearDesconto(txtDescontoTotal.Text.ToDecimalOrNull().GetValueOrDefault());
               CalcularDescontoNosTributos();
            }
            CalcularTotais(null);
         }
      }
      private void RatearDesconto(decimal valorDescontoTotal)
      {
         foreach (var nfeProduto in nfeProdutoObservable)
         {
            var valorDescontoRateio = Math.Round(valorDescontoTotal / nfeProdutoObservable
              .Sum(x => Math.Round(x.ValorUnitario, 4) * x.Quantidade) *
              (Math.Round(nfeProduto.ValorUnitario, 4) * nfeProduto.Quantidade), 4);
            nfeProduto.ValorDesconto = valorDescontoRateio;
         }
      }
      private void uscCSTPIS_EventoCodigoAlterado(object sender, CodigoAlteradoArgs e)
      {
         if (uscCSTPIS.Id.In(1, 2, 3, 49, 99))
         {
            txtBasePIS.IsEnabled = true;
            txtAliquotaPIS.IsEnabled = true;
            txtValorPIS.IsEnabled = true;
         }
         else
         {
            txtBasePIS.IsEnabled = false;
            txtAliquotaPIS.IsEnabled = false;
            txtValorPIS.IsEnabled = false;
            txtBasePIS.Text = string.Empty;
            txtAliquotaPIS.Text = string.Empty;
            txtValorPIS.Text = string.Empty;
         }
      }

      private void uscCSTCOFINS_EventoCodigoAlterado(object sender, CodigoAlteradoArgs e)
      {
         if (uscCSTCOFINS.Id.In(1, 2, 3, 49, 99))
         {
            txtBaseCOFINS.IsEnabled = true;
            txtAliquotaCOFINS.IsEnabled = true;
            txtValorCOFINS.IsEnabled = true;
         }
         else
         {
            txtBaseCOFINS.IsEnabled = false;
            txtAliquotaCOFINS.IsEnabled = false;
            txtValorCOFINS.IsEnabled = false;
            txtBaseCOFINS.Text = string.Empty;
            txtAliquotaCOFINS.Text = string.Empty;
            txtValorCOFINS.Text = string.Empty;
         }
      }

      private void uscCSTIPI_EventoCodigoAlterado(object sender, CodigoAlteradoArgs e)
      {
         if (uscCSTIPI.Id.In(50, 99))
         {
            txtBaseIPI.IsEnabled = true;
            txtAliquotaIPI.IsEnabled = true;
            txtValorIPI.IsEnabled = true;
         }
         else
         {
            txtBaseIPI.IsEnabled = false;
            txtAliquotaIPI.IsEnabled = false;
            txtValorIPI.IsEnabled = false;
            txtBaseIPI.Text = string.Empty;
            txtAliquotaIPI.Text = string.Empty;
            txtValorIPI.Text = string.Empty;
         }
      }
      private void btnSalvar_Click(object sender, RoutedEventArgs e)
      {
         ValidarCamposObrigatorioTransporte();
         SalvarNota();
      }
      private void TxtPorcentagemFCP_SelectionChanged(object sender, RoutedEventArgs e)
      {
         decimal valorBase = txtBasePartilhaICMS.Text.ToDecimal();
         decimal PercentualFcp = txtPorcentagemFCP.Text.ToDecimal();
         decimal valorFcp;
         valorFcp = valorBase * PercentualFcp / 100;
         txtValorFCP.Text = valorFcp.ToString("N2");
      }

      #region Aba Transportadora
      private void PreencherCamposFrete()
      {
         //Groupbox Frete - Colocar dentro do método que irá preencher a nf
         var nfFrete = nfe.tb_nfe_frete;
         cboTipoFrete.SelectedValue = (ModalidadeFrete)nfFrete.idTipoFrete;
         txtValorFrete.Text = nfFrete.ValorFrete.ToStringOrNull();
         txtQuantidadeVolumesFrete.Text = nfFrete.QuantidadeVolumes.ToStringOrNull();
         txtEspecieVolumes.Text = nfFrete.EspecieVolumes;
         txtMarca.Text = nfFrete.Marca;
         txtNumeracao.Text = nfFrete.Numeracao;
         txtPesoBruto.Text = nfFrete.PesoBruto.ToStringOrNull();
         txtPesoLiquido.Text = nfFrete.PesoLiquido.ToStringOrNull();

         //Groupbox Transportadora
         uscTransportadora.Id = nfFrete.idTransportadora;
         uscMotorista.Id = nfFrete.idMotorista;
         cboPlacaVeiculo.SelectedValue = nfFrete.idVeiculo;
      }
      private void PreencherTransportadora(int idTransportadora)
      {
         var transportadora = new TransportadoraBusiness().BuscarPorId(idTransportadora);
         if (transportadora != null)
         {
            txtInscricaoEstadualTransportadora.Text = transportadora.InscricaoEstadual;
            txtNumeroInscricaoTransportadora.Text = transportadora.NumeroInscricao;
            DefinirMascaraTipoInscricao(transportadora.TipoInscricao.ToInt32(), txtNumeroInscricaoTransportadora);
            txtEnderecoTransportadora.Text = transportadora.Endereco;
            txtBairroTransportadora.Text = transportadora.Bairro;
            txtCidadeTransportadora.Text = transportadora.Cidade;
            txtEstadoTransportadora.Text = transportadora.UF;
            txtCEPTransportadora.Text = transportadora.CEP;
            txtTelefoneTransportadora.Text = transportadora.TelefoneCompleto;
         }
         else
         {
            txtInscricaoEstadualTransportadora.Text = string.Empty;
            txtNumeroInscricaoTransportadora.Text = string.Empty;
            txtEnderecoTransportadora.Text = string.Empty;
            txtBairroTransportadora.Text = string.Empty;
            txtCidadeTransportadora.Text = string.Empty;
            txtEstadoTransportadora.Text = string.Empty;
            txtCEPTransportadora.Text = string.Empty;
            txtTelefoneTransportadora.Text = string.Empty;
            new MotoristaConfiguracaoHelper().Desconfigurar();
            cboPlacaVeiculo.ItemsSource = null;
         }
      }
      private void HabilitarCamposTransportadora()
      {
         if (cboTipoFrete.SelectedIndex == ModalidadeFrete.DestinatárioRemetente.ToInt32())
         {
            txtValorFrete.IsEnabled = true;
            txtQuantidadeVolumesFrete.IsEnabled = true;
            txtEspecieVolumes.IsEnabled = true;
            txtMarca.IsEnabled = true;
            txtNumeracao.IsEnabled = true;
            txtPesoBruto.IsEnabled = true;
            txtPesoLiquido.IsEnabled = true;
         }
         else
         {
            txtValorFrete.IsEnabled = false;
            txtQuantidadeVolumesFrete.IsEnabled = false;
            txtEspecieVolumes.IsEnabled = false;
            txtMarca.IsEnabled = false;
            txtNumeracao.IsEnabled = false;
            txtPesoBruto.IsEnabled = false;
            txtPesoLiquido.IsEnabled = false;

            txtValorFrete.Text = string.Empty;
            txtQuantidadeVolumesFrete.Text = string.Empty;
            txtEspecieVolumes.Text = string.Empty;
            txtMarca.Text = string.Empty;
            txtNumeracao.Text = string.Empty;
            txtPesoBruto.Text = string.Empty;
            txtPesoLiquido.Text = string.Empty;
         }

         if (cboTipoFrete.SelectedValue != null && cboTipoFrete.SelectedValue.ToString().In(ModalidadeFrete.SemFrete.ToString(), ModalidadeFrete.Emitente.ToString()))
         {
            uscTransportadora.IsEnabled = false;
            uscTransportadora.Id = null;
            txtValorFrete.MensagemObrigatoria = string.Empty;
            txtQuantidadeVolumesFrete.MensagemObrigatoria = string.Empty;
            txtEspecieVolumes.MensagemObrigatoria = string.Empty;
            txtMarca.MensagemObrigatoria = string.Empty;
            txtNumeracao.MensagemObrigatoria = string.Empty;
            txtPesoBruto.MensagemObrigatoria = string.Empty;
            txtPesoLiquido.MensagemObrigatoria = string.Empty;
            uscTransportadora.MensagemObrigatoria = string.Empty;
            txtNumeroInscricaoTransportadora.MensagemObrigatoria = string.Empty;
            txtEnderecoTransportadora.MensagemObrigatoria = string.Empty;
            txtBairroTransportadora.MensagemObrigatoria = string.Empty;
            txtCidadeTransportadora.MensagemObrigatoria = string.Empty;
            txtEstadoTransportadora.MensagemObrigatoria = string.Empty;

         }
         else
         {
            uscTransportadora.IsEnabled = true;
            txtValorFrete.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtQuantidadeVolumesFrete.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtEspecieVolumes.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtMarca.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtNumeracao.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtPesoBruto.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtPesoLiquido.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            uscTransportadora.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtNumeroInscricaoTransportadora.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtEnderecoTransportadora.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtBairroTransportadora.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtCidadeTransportadora.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
            txtEstadoTransportadora.MensagemObrigatoria = Constants.MENSAGEM_ATENCAO_CAMPOS_OBRIGATORIOS;
         }
      }
      private void cboTipoFrete_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         HabilitarCamposTransportadora();
      }
      private void CarregarComboPlacaVeiculo(int idTransportadora)
      {
         var lista = new TransportadoraVeiculoBusiness().BuscarPorIdTransportadora(idTransportadora);
         if (lista != null)
         {
            cboPlacaVeiculo.DisplayMemberPath = "Placa";
            cboPlacaVeiculo.SelectedValuePath = "id";
            cboPlacaVeiculo.ItemsSource = lista;
         }
         else
            cboPlacaVeiculo.ItemsSource = null;
         lista = null;
      }

      private void uscTransportadora_EventoCodigoAlterado_1(object sender, CodigoAlteradoArgs e)
      {
         if (uscTransportadora.Id.HasValue)
            PreencherTransportadora(uscTransportadora.Id.Value);
         uscMotorista.IsEnabled = false;
         var txt = sender as usrConsultaControle;
         if (txt != null && txt.Id.HasValue)
         {
            CarregarComboPlacaVeiculo(txt.Id.Value);
            if (uscTransportadora.Id.HasValue && uscTransportadora.Id.Value > 0)
               new MotoristaConfiguracaoHelper().Configurar(uscMotorista, uscTransportadora.Id);
            uscMotorista.IsEnabled = true;
         }
         else
         {
            cboPlacaVeiculo.ItemsSource = null;
            txtInscricaoEstadualTransportadora.Text = string.Empty;
            txtEnderecoTransportadora.Text = string.Empty;
            txtCEPTransportadora.Text = string.Empty;
            txtBairroTransportadora.Text = string.Empty;
            txtCidadeTransportadora.Text = string.Empty;
            txtEstadoTransportadora.Text = string.Empty;
            txtTelefoneTransportadora.Text = string.Empty;
            txtNumeroInscricaoTransportadora.Text = string.Empty;
            uscMotorista.Id = null;
         }
      }
      private void ValidarCamposObrigatorioTransporte()
      {
         ControleNavegador navegador = new ControleNavegador();
         Utilidades.ValidarCamposObrigatorios(gbxAbaTransporte, navegador);

         if (navegador.ExisteErroRegistro)
            throw new BusinessException(PCInfo.Utils.Constants.CAMPOS_OBRIGATORIOS);
      }
      #endregion

      #region Aba Pagamento
      private void cboFormaPagamento_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         if (cboFormaPagamento.SelectedValue.ToInt32() == (int)IndicadorPagamento.ipVista)
         {
            btnAdicionarParcelas.IsEnabled = false;
            formaPagamentoObservable.Clear();
            dgParcelas.ItemsSource = null;
            this.dgFormaPagamento.ItemsSource = null;
            pagamentoObservable.Clear();
            txtValorRestante.Text = txtValorTotalPagamento.Text;
         }
         else if (cboFormaPagamento.SelectedValue.ToInt32() == (int)IndicadorPagamento.ipPrazo)
         {
            btnAdicionarParcelas.IsEnabled = true;
            this.dgFormaPagamento.ItemsSource = null;
            formaPagamentoObservable.Clear();
         }
         else
         {
            btnAdicionarParcelas.IsEnabled = false;
            dgParcelas.ItemsSource = null;
            formaPagamentoObservable.Clear();
         }
      }

      private void btnAdicionarFormaPagamento_Click(object sender, RoutedEventArgs e)
      {
         decimal ValorRestante = txtValorTotalPagamento.Text.ToDecimal() - formaPagamentoObservable.Sum(x => x.ValorDoPagamento).Value;
         if (nfeProdutoObservable.Count == 0)
            throw new BusinessException("Favor Inserir o Produto!");
         decimal QuantidadeProduto = nfeProdutoObservable.FirstOrDefault().Quantidade;
         if (ValorRestante.ToString().ToStringReplaceMonetario().ToDecimal() <= 0 && formaPagamentoObservable.Count > 0)
            throw new BusinessException("Não é possível adicionar mais formas de pagamento pois o valor total já foi informado.");
         else if ((ValorRestante.ToString().ToStringReplaceMonetario().ToDecimal() <= 0 && formaPagamentoObservable.Count <= 0 && QuantidadeProduto != 0))
            throw new BusinessException("Não é possível adicionar mais forma de pagamento pois não existe valor em aberto.");

         var frmAdicionarFormaDePagamentoNota = new frmAdicionarFormaDePagamentoNota(formaPagamentoObservable, ValorRestante.ToString().ToStringReplaceMonetario().ToDecimal(), (int)cboFormaPagamento.SelectedValue);
         if (frmAdicionarFormaDePagamentoNota.ShowDialog().Value)
         {
            if ((formaPagamentoObservable.Sum(x => x.ValorDoPagamento) + frmAdicionarFormaDePagamentoNota.NFePagamento.ValorDoPagamento) > txtValorTotalPagamento.Text.ToStringReplaceMonetario().ToDecimal())
               throw new BusinessException("Não foi possível adicionar a forma de pagamento pois a soma dos valores é maior que o valor total da nota.");
            formaPagamentoObservable.Add(frmAdicionarFormaDePagamentoNota.NFePagamento);

            foreach (var item in formaPagamentoObservable)
            {
               var descicao = new FormaPagamentoBusiness().BuscarPorIdDescricao(item.FormaDePagamento);
               nfe.FormaDePagamento = item.FormaDePagamento.ToInt32();
               nfe.FormaPagamento = descicao.id;
               item.FormaDePagamento = item.FormaDePagamento;
               item.idNFe = nfe.id;
               item.FormaDePagamentoDecricao = descicao.DescricaoFormaPagamentoNFe;
            }

            dgFormaPagamento.ItemsSource = null;
            dgFormaPagamento.ItemsSource = formaPagamentoObservable;
         }
      }

      private void btnAdicionarParcelas_Click(object sender, RoutedEventArgs e)
      {
         if (txtValorRestante.Text.ToStringReplaceMonetario().ToDecimal() <= 0 && pagamentoObservable.Count > 0)
            throw new BusinessException("Não é possível adicionar mais parcelas pois o valor total já foi informado.");
         else if ((txtValorRestante.Text.ToStringReplaceMonetario().ToDecimal() <= 0 && pagamentoObservable.Count <= 0))
            throw new BusinessException("Não é possível adicionar mais parcelas pois não existe valor em aberto.");

         var frmAdicionarPagamentoNota = new frmAdicionarPagamentoNota(pagamentoObservable.Count + 1, txtValorRestante.Text.ToStringReplaceMonetario().ToDecimal());
         if (frmAdicionarPagamentoNota.ShowDialog().Value)
         {
            if ((pagamentoObservable.Sum(x => x.Valor) + frmAdicionarPagamentoNota.NFePagamentoLista.Sum(x => x.Valor)) > txtValorTotalPagamento.Text.ToStringReplaceMonetario().ToDecimal())
               throw new BusinessException("Não foi possível adicionar a parcela pois a soma dos valores é maior que o valor total da nota.");

            //abater valor inserido do grid ao valor restante
            decimal valorRestante = txtValorRestante.Text.ToDecimal();
            decimal calculo = valorRestante - frmAdicionarPagamentoNota.NFePagamentoLista.Sum(x => x.Valor);
            txtValorRestante.Text = calculo.ToString("N2");
            ////
            int NumeroParcelasNoGrid = pagamentoObservable.Count;
            foreach (var item in frmAdicionarPagamentoNota.NFePagamentoLista)
            {
               NumeroParcelasNoGrid++;
               if (pagamentoObservable.Count > 0)
                  item.Numero = NumeroParcelasNoGrid;
               pagamentoObservable.Add(item);
            }
            if (cboFormaPagamento.SelectedValue.ToInt32() == (int)IndicadorPagamento.ipPrazo && txtValorRestante.Text != "R$ 0,00")
               btnAdicionarParcelas.IsEnabled = true;
            else
               btnAdicionarParcelas.IsEnabled = false;

            dgParcelas.ItemsSource = null;
            dgParcelas.ItemsSource = pagamentoObservable;
         }
      }

      private void dgParcelas_MouseDoubleClick(object sender, MouseButtonEventArgs e)
      {
         if (nfe != null && (nfe.idNFStatus != (int)EnumStatusNFe.Cancelada
             && nfe.idNFStatus != (int)EnumStatusNFe.EmitidaComSucesso &&
             nfe.idNFStatus != (int)EnumStatusNFe.Inutilizada &&
             nfe.idNFStatus != (int)EnumStatusNFe.CanceladaSemEmissao))
         {
            List<tb_nfe_pagamento> nfePagamentoLista = new List<tb_nfe_pagamento>();
            var nfePagamento = dgParcelas.SelectedItem as tb_nfe_pagamento;
            foreach (var item in pagamentoObservable)
            {
               nfePagamentoLista.Add(item);
            }
            if (nfePagamento != null)
            {
               var valorAntigo = nfePagamento.Valor;
               var lancamento = new frmAdicionarPagamentoNota(nfePagamentoLista, txtValorRestante.Text.ToStringReplaceMonetario().ToDecimal());
               lancamento.ShowDialog();
               if (lancamento.NFePagamentoLista != null)
               {
                  pagamentoObservable.Clear();
                  foreach (var item in lancamento.NFePagamentoLista)
                  {
                     pagamentoObservable.Add(item);
                  }
               }
               if (pagamentoObservable.Sum(x => x.Valor) > txtValorTotalPagamento.Text.ToStringReplaceMonetario().ToDecimal())
               {
                  nfePagamento.Valor = valorAntigo;
                  throw new BusinessException("Não foi possível editar a parcela pois a soma dos valores é maior que o valor total da nota.");
               }
               foreach (var item in pagamentoObservable)
               {
                  if (cboFormaPagamento.SelectedValue.ToInt32() == (int)IndicadorPagamento.ipPrazo && item.Valor == txtValorTotalPagamento.Text.ToDecimal())
                     btnAdicionarParcelas.IsEnabled = false;
                  else
                     btnAdicionarParcelas.IsEnabled = true;
               }

               dgParcelas.ItemsSource = null;
               dgParcelas.ItemsSource = pagamentoObservable;
            }
         }
      }

      private void dgFormaPagamento_MouseDoubleClick(object sender, MouseButtonEventArgs e)
      {
         if (nfe != null && (nfe.idNFStatus != (int)EnumStatusNFe.Cancelada
           && nfe.idNFStatus != (int)EnumStatusNFe.EmitidaComSucesso &&
           nfe.idNFStatus != (int)EnumStatusNFe.Inutilizada &&
           nfe.idNFStatus != (int)EnumStatusNFe.CanceladaSemEmissao))
         {
            var nfeFormaDePagamento = dgFormaPagamento.SelectedItem as tb_nfe_formapagamento;
            if (nfeFormaDePagamento != null)
            {
               var valorAntigo = nfeFormaDePagamento.ValorDoPagamento;
               var lancamento = new frmAdicionarFormaDePagamentoNota(nfeFormaDePagamento, (int)cboFormaPagamento.SelectedValue);
               lancamento.ShowDialog();
               if (formaPagamentoObservable.Sum(x => x.ValorDoPagamento) > txtValorTotalPagamento.Text.ToStringReplaceMonetario().ToDecimal())
               {
                  nfeFormaDePagamento.ValorDoPagamento = valorAntigo;
                  throw new BusinessException("Não foi possível editar a forma de pagamento pois a soma dos valores é maior que o valor total da nota.");
               }

               ObservableCollection<tb_nfe_formapagamento> formaPagamentoObservableTemp = new ObservableCollection<tb_nfe_formapagamento>();
               foreach (tb_nfe_formapagamento item in formaPagamentoObservable)
               {
                  if (item.id == nfeFormaDePagamento.id)
                  {
                     var d = new FormaPagamentoBusiness().BuscarPorIdDescricao(item.FormaDePagamento);
                     item.FormaDePagamentoDecricao = d.DescricaoFormaPagamentoNFe;
                  }
                  formaPagamentoObservableTemp.Add(item);
               }
               dgFormaPagamento.ItemsSource = null;
               dgFormaPagamento.ItemsSource = formaPagamentoObservableTemp;

               //decimal total = txtValorTotalPagamento.Text.ToStringReplaceMonetario().ToDecimal();
               //txtValorRestante.Text = Math.Round(total, 2).ToString();
            }
         }
      }

      private void dgParcelas_PreviewKeyDown(object sender, KeyEventArgs e)
      {
         if (MessageBoxUtils.ExibeMensagemQuestion("Deseja Realmente Excluir o Registro?"))
         {
            var valorSelecionado = dgParcelas.SelectedItem as tb_nfe_pagamento;
            decimal valorCampo = txtValorRestante.Text.ToDecimal();
            decimal calculo = valorSelecionado.Valor + valorCampo;
            txtValorRestante.Text = calculo.ToString("N2");
         }
         else { e.Handled = true; };
      }
      private void dgFormaPagamento_PreviewKeyDown(object sender, KeyEventArgs e)
      {
         if (MessageBoxUtils.ExibeMensagemQuestion("Deseja Realmente Excluir o Registro?"))
         {
            var valorSelecionado = dgFormaPagamento.SelectedItem as tb_nfe_formapagamento;
            formaPagamentoObservable.Remove(valorSelecionado);
         }
         else { e.Handled = true; };
      }

      #endregion

      #region Aba Informações Complementares

      private void InformacoesObrigatorias(ObservableCollection<tb_nfe_produto> nfeProdutoObservable)
      {
         string mensagemPadra = "";
         if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
            mensagemPadra = "DOCUMENTO EMITIDO POR ME OU EPP OPTANTE PELO SIMPLES NACIONAL;\n";
         if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional && cboTipoNota.SelectedValue.ToInt32() != (int)EnumTipoNota.DevolucaoCompra && nfeProdutoObservable.FirstOrDefault().idCSOSN != 101)
            mensagemPadra += "NÃO GERA DIREITO A CRÉDITO FISCAL DE ICMS, DE ISS E DE IPI.\n";
         if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional && nfeProdutoObservable.FirstOrDefault().idCSOSN == 101)
         {
            decimal? valorIcms = nfeProdutoObservable.Sum(x => x.ValorICMS);
            decimal? aliquotaIcms = nfeProdutoObservable.FirstOrDefault().ValorAliquotaICMS;
            mensagemPadra += "PERMITE O APROVEITAMENTO DO CRÉDITO DE ICMS NO VALOR DE R$" + valorIcms + "; CORRESPONDENTE À ALÍQUOTA DE " + aliquotaIcms + "%, NOS TERMOS DO ART. 23 DA LEI COMPLEMENTAR Nº 123, DE 2006";
         }
         txtInfComplementaresPadrao.Text = mensagemPadra;

         if (empresa.tb_cidade.tb_estado.idPais == estadoCliente.idPais)
            txtImpostoAproximado.Text = nfeBusiness.PreencherImpostosAproximados(nfeProdutoObservable.ToList());
      }
      private void cboInformacoesComplementares_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
      {
         if (cboInformacoesComplementares.SelectedItem != null)
            txtInfEditaveis.Text = (cboInformacoesComplementares.SelectedItem as tb_informacoes_complementares_nfe).Descricao;
         if (uscMotorista.Id != null && uscMotorista.Id.Value > 0)
         {
            var motorista = new MotoristaBusiness().BuscarPorId(uscMotorista.Id.Value);
            if (motorista != null)
               txtInfEditaveis.Text = txtInfComplementaresPadrao.Text.Insert(0, "Motorista: " + motorista.Nome + " ");
         }
      }
      #endregion

      #region Aba Referencias NFe

      private void CarregarComboEspecieVolume()
      {
         var lista = new EspecieBusiness().BuscarTodos();
         if (lista != null)
         {
            cboModeloEspecie.DisplayMemberPath = "Nome";
            cboModeloEspecie.SelectedValuePath = "id";
            cboModeloEspecie.ItemsSource = lista.Where(x => x.id.In(1, 4));
            cboModeloEspecie.SelectedValue = 4;
         }
         else
            cboModeloEspecie.ItemsSource = null;
         lista = null;
      }
      private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
      {
         tb_nfe_referencia_contranota contranota;
         contranota = dgContranotaReferencia.SelectedItem as tb_nfe_referencia_contranota;
         if (contranota != null && MessageBoxUtils.ExibeMensagemQuestion("Deseja Realmente Excluir o Registro!"))
         {
            nfeReferenciaContranotaObservable.Remove(contranota);

         }
         else { e.Handled = true; };

      }
      private void BtnAdicionarReferenciaContranota_Click(object sender, RoutedEventArgs e)
      {

         tb_nfe_referencia_contranota contranota = new tb_nfe_referencia_contranota();
         contranota.NumeroNota = txtNumeroNotaReferencia.Text.ToIntOrNull();
         contranota.Serie = txtNumeroSerieReferencia.Text.ToIntOrNull();
         contranota.DataEmissao = txtDataReferencia.Text.ToDateTime();
         contranota.idEspecie = cboModeloEspecie.SelectedValue.ToIntOrNull();
         ValidarCamposObrigatorioReferenciaContranota(contranota);
         nfeReferenciaContranotaObservable.Add(contranota);
         dgContranotaReferencia.ItemsSource = nfeReferenciaContranotaObservable;
         txtNumeroNotaReferencia.Text = string.Empty;
         txtNumeroSerieReferencia.Text = string.Empty;
      }
      private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
      {

      }
      private void rdbDevolucaoNota_Checked(object sender, RoutedEventArgs e)
      {
         tbiNfe.IsEnabled = true;
         txtchaveReferencia.IsEnabled = true;
         btnAdicionarChave.IsEnabled = true;
         tbiCupom.IsEnabled = false;
         txtNumeroSequencial.IsEnabled = false;
         txtCoo.IsEnabled = false;
         btnIncluirCupom.IsEnabled = false;
         btnExcluirCupom.IsEnabled = false;
      }
      private void RdbContranotaProdutor_Checked(object sender, RoutedEventArgs e)
      {

      }
      private void rdbDevolucaoCupom_Checked(object sender, RoutedEventArgs e)
      {
         tbiNfe.IsEnabled = false;
         txtchaveReferencia.IsEnabled = false;
         btnAdicionarChave.IsEnabled = false;
         tbiCupom.IsEnabled = true;
         txtNumeroSequencial.IsEnabled = true;
         txtCoo.IsEnabled = true;
         btnIncluirCupom.IsEnabled = true;
         btnExcluirCupom.IsEnabled = true;
      }

      private void dgChaveReferencia_PreviewKeyDown(object sender, KeyEventArgs e)
      {
         var itemSelecionado = dgChaveReferencia.SelectedItem as tb_nfe_referencia;
         if (itemSelecionado != null && MessageBoxUtils.ExibeMensagemQuestion("Deseja Excluir a Referência?"))
         {
            nfeReferenciaObservable.Remove(itemSelecionado);
         }
         else { e.Handled = true; };
      }

      private void dgChaveReferencia_MouseDoubleClick(object sender, MouseButtonEventArgs e)
      {
         var itemSelecionado = dgChaveReferencia.SelectedItem as tb_nfe_referencia;
         if (itemSelecionado != null)
         {
            txtchaveReferencia.Text = itemSelecionado.Chave;
            nfeReferenciaObservable.Remove(itemSelecionado);
         }
      }


      private void btnAdicionarChave_Click_1(object sender, RoutedEventArgs e)
      {
         if (string.IsNullOrEmpty(txtchaveReferencia.Text) || txtchaveReferencia.Text.Length < 44)
            throw new BusinessException("Chave de referência inválida!");

         var nfeReferencia = new tb_nfe_referencia();
         nfeReferencia.idNFe = nfe.id;
         nfeReferencia.Chave = txtchaveReferencia.Text;

         if (nfeReferenciaObservable.Where(x => x.Chave == nfeReferencia.Chave).Any())
            throw new BusinessException(string.Format("Chave de referência {0} já adicionada.", nfeReferencia.Chave));
         else
            nfeReferenciaObservable.Add(nfeReferencia);

         txtchaveReferencia.Text = string.Empty;
         txtchaveReferencia.Focus();
         dgChaveReferencia.ItemsSource = null;
         dgChaveReferencia.ItemsSource = nfeReferenciaObservable;
      }

      private void btnIncluirCupom_Click(object sender, RoutedEventArgs e)
      {
         tb_nfe_referencia_cupom cupom = new tb_nfe_referencia_cupom();

         cupom.Modelo = cboModeloEcf.SelectedValue.ToString();

         cupom.NumeroECF = txtNumeroSequencial.Text.ToLong();
         cupom.NumeroCupom = txtCoo.Text.ToLong();

         ValidarCamposObrigatorioReferenciaCupom(cupom);
         nfeReferenciaCupomObservable.Add(cupom);
         dgReferenciaCupom.ItemsSource = nfeReferenciaCupomObservable;
         txtNumeroSequencial.Text = null;
         txtCoo.Text = null;
      }

      private void btnExcluirCupom_Click(object sender, RoutedEventArgs e)
      {
         var itemSelecionado = dgReferenciaCupom.SelectedItem as tb_nfe_referencia_cupom;
         if (itemSelecionado != null && MessageBoxUtils.ExibeMensagemQuestion("Deseja Excluir a Referência?"))
         {
            nfeReferenciaCupomObservable.Remove(itemSelecionado);
         }
         else { e.Handled = true; };
      }

      private void dgReferenciaCupom_KeyDown(object sender, KeyEventArgs e)
      {

      }

      private void dgReferenciaCupom_PreviewKeyDown(object sender, KeyEventArgs e)
      {
         var itemSelecionado = dgReferenciaCupom.SelectedItem as tb_nfe_referencia_cupom;
         if (itemSelecionado != null && MessageBoxUtils.ExibeMensagemQuestion("Deseja Excluir a Referência?"))
         {
            nfeReferenciaCupomObservable.Remove(itemSelecionado);
         }
         else { e.Handled = true; };
      }
      private void ValidarCamposObrigatorioReferenciaCupom(tb_nfe_referencia_cupom cupom)
      {
         if (cupom.NumeroCupom <= 0)
         {
            txtCoo.Style = FindResource("boldStyle") as Style;
            throw new BusinessException(Constants.CAMPOS_OBRIGATORIOS);
         }
         if (cupom.NumeroECF <= 0)
         {
            txtNumeroSequencial.Style = FindResource("boldStyle") as Style;
            throw new BusinessException(Constants.CAMPOS_OBRIGATORIOS);
         }

      }
      private void ValidarCamposObrigatorioReferenciaContranota(tb_nfe_referencia_contranota contranota)
      {
         if (contranota.NumeroNota <= 0 || contranota.NumeroNota == null)
         {
            txtNumeroNotaReferencia.Style = FindResource("boldStyle") as Style;
            throw new BusinessException(Constants.CAMPOS_OBRIGATORIOS);
         }
         if (contranota.Serie < 0 || contranota.Serie == null)
         {
            txtNumeroSerieReferencia.Style = FindResource("boldStyle") as Style;
            throw new BusinessException(Constants.CAMPOS_OBRIGATORIOS);
         }

      }
      private void dgReferenciaCupom_MouseDoubleClick(object sender, MouseButtonEventArgs e)
      {
         var itemSelecinado = dgReferenciaCupom.SelectedItem as tb_nfe_referencia_cupom;
         if (itemSelecinado != null)
         {
            cboModeloEcf.SelectedValue = itemSelecinado.Modelo;
            txtNumeroSequencial.Text = itemSelecinado.NumeroECF.ToString();
            txtCoo.Text = itemSelecinado.NumeroCupom.ToString();

            nfeReferenciaCupomObservable.Remove(itemSelecinado);
         }
      }
      #endregion

      #region Aba Erros
      private void btnConsultaCEST_Click(object sender, RoutedEventArgs e)
      {
         if (dgNFErros.SelectedItem != null)
         {
            var itemSelecionado = dgNFErros.SelectedItem as tb_nf_erros;
            if (itemSelecionado.Codigo == 806)
            {
               List<tb_nfe_produto> listaProdutos = new List<tb_nfe_produto>();
               foreach (var item in nfeProdutoObservable)
               {
                  item.tb_produto = new ProdutoBusiness().BuscarPorId(item.idProduto);
                  listaProdutos.Add(item);

               }
               var listaSemCEST = listaProdutos.Where(x => string.IsNullOrEmpty(x.tb_produto.CEST)).ToList();
               bool excluir = true;
               foreach (var nfeProduto in listaSemCEST)
               {
                  if (MessageBoxUtils.ExibeMensagemQuestion(string.Format("Produto {0} não possui CEST informado, deseja inserir agora?", nfeProduto.tb_produto.Descricao)))
                  {
                     var produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);
                     var listaCEST = new PCInfo.Utils.ConsultaCEST().ConsultaCestPorNCM(produto.NCM);
                     frmSelecionarCEST frm = new frmSelecionarCEST(listaCEST);
                     if (frm.ShowDialog().Value)
                     {
                        var cestSelecionado = frm.CESTSelecionado.Replace(".", "").ToString().Trim();
                        produtoBusiness.AtualizarCEST(produto.id, cestSelecionado);
                        if (excluir)
                        {
                           new NFErrosBusiness().Excluir(itemSelecionado);
                           excluir = false;
                        }
                        MessageBoxUtils.ExibeMensagemSucesso("CEST Atualizado com Sucesso!");
                     }
                  }
               }
               PreencherCampos();
               // tbiCabecalho.SelectedItem = tabCabecalho;
            }
         }
      }
      public void PreencherErros()
      {
         var listaErros = new NFErrosBusiness().BuscarPorIdNF(nfe.id);
         if (listaErros != null && listaErros.Any())
         {
            tbiErros.Visibility = Visibility.Visible;
            tbiErros.Focus();
            dgNFErros.ItemsSource = null;
            dgNFErros.ItemsSource = listaErros.OrderByDescending(x => x.Data);
         }
         else
            tbiErros.Visibility = Visibility.Collapsed;
      }


      private void btnConsultaNCM_Click(object sender, RoutedEventArgs e)
      {
         if (dgNFErros.SelectedItem != null)
         {
            var itemSelecionado = dgNFErros.SelectedItem as tb_nf_erros;
            if (itemSelecionado.Codigo == 778)
            {
               var numeroItem = itemSelecionado.Mensagem.Replace("Rejeicao: Informado NCM inexistente - [nItem: ", "").Replace("]", "").ToInt32();
               var nfeProduto = nfeProdutoObservable.Where(x => x.NumeroItem == numeroItem).FirstOrDefault();
               var produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);
               usrConsultaNCM frmConsultaNCM = new usrConsultaNCM(produto.Descricao);
               if (frmConsultaNCM.ShowDialog().Value)
               {
                  var ncmSelecionado = frmConsultaNCM.NCM;
                  if (MessageBoxUtils.ExibeMensagemQuestion(string.Format("Deseja inserir o NCM {0} para o produto {1}?",
                    ncmSelecionado, produto.Descricao)))
                  {
                     produtoBusiness.AtualizarNCM(produto.id, ncmSelecionado);
                     new NFErrosBusiness().Excluir(itemSelecionado);
                     PreencherCampos();
                     //tabPrincipal.SelectedItem = tabCabecalho;
                     MessageBoxUtils.ExibeMensagemSucesso("NCM Atualizado com Sucesso!");
                  }
               }
            }
         }
      }
      private void DgNFErros_MouseDoubleClick(object sender, MouseButtonEventArgs e)
      {
         if (dgNFErros.SelectedItem != null)
         {
            var itemSelecionado = dgNFErros.SelectedItem as tb_nf_erros;
            if (itemSelecionado.idNF.HasValue)
            {
               var xmlRetorno = nfeBusiness.BuscarPorId(itemSelecionado.idNF.Value);
               if (xmlRetorno != null)
               {
                  var frmConsultaErrosNFeOnline = new frmConsultaErrosNFeOnline(xmlRetorno);
                  frmConsultaErrosNFeOnline.ShowDialog();
               }
            }
         }
      }

      private void DgNFErros_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         btnConsultaCEST.IsEnabled = false;
         btnConsultaCEST.IsEnabled = false;
         if (dgNFErros.SelectedItem != null)
         {
            var itemSelecionado = dgNFErros.SelectedItem as tb_nf_erros;
            if (itemSelecionado.Codigo == 778)
               btnConsultaNCM.IsEnabled = true;
            else if (itemSelecionado.Codigo == 806)
               btnConsultaCEST.IsEnabled = true;
         }
      }
      #endregion

      #region  Salvar dados nota
      private void SalvarNota()
      {
         decimal valorDoPagmento = formaPagamentoObservable.Sum(v => v.ValorDoPagamento).GetValueOrDefault();
         string sValorTotal = txtValorTotalNota.Text.ToDecimal().ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
         string svalorDoPagmento = valorDoPagmento.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
         decimal valorDoPagementoRestante = sValorTotal.ToDecimal() - svalorDoPagmento.ToDecimal();
         string Zerado = Convert.ToDecimal(0.00).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);
         decimal valorGridPagamento = pagamentoObservable.Sum(x => x.Valor);
         decimal valorRestante = txtValorTotalPagamento.Text.ToDecimal();
         string sValorRestante = (valorGridPagamento - valorRestante).ToString("N2");
         string sZerado = Convert.ToDecimal(0.00).ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO);


         //validações padrão
         if (cboFormaPagamento.SelectedValue.ToInt32() == (int)IndicadorPagamento.ipPrazo && pagamentoObservable.Count == 0)
            throw new BusinessException("É necesário pelo menos uma parcela.");

         else if (cboFormaPagamento.SelectedValue.ToInt32() == (int)IndicadorPagamento.ipPrazo && sValorRestante != sZerado)
            throw new BusinessException("Ainda existe valores restantes de Parcelas para serem adicionados. " + sValorRestante);

         else if (formaPagamentoObservable.Count == 0)
            throw new BusinessException("É necessário preencher o grid Forma de Pagamento");

         else if (valorDoPagementoRestante != Zerado.ToDecimal())
         {
            throw new BusinessException("Existe diferença entre o valor total da nota e, o valor informado na aba Pagamento, favor corrigir a aba Pagamento deixando a mesma com o mesmo valor do Total da Nota. " + valorDoPagementoRestante.ToString(Constants.FORMATO_CASAS_DECIMAIS_MONETARIO));
         }
         else if (empresa != null && empresa.InscricaoEstadual == string.Empty)
            throw new BusinessException("É necessário Preencher a Inscrição Estadual!");

         foreach (var item in nfeProdutoObservable)
         {
            if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional && item.idCSOSN == null)
               throw new BusinessException("É necessário informar o CSOSN para o(s) produto(s): " + item.tb_produto.Descricao);
            if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional && item.idCSTICMS == null)
               throw new BusinessException("É necessário informar o CST para o(s) produto(s): " + item.tb_produto.Descricao);
         }
         var dataSaida = txtDataSaida.Text.ToDateTime();
         var dataEmissao = txtDataEmissao.Text.ToDateTime();
         if (dataSaida < dataEmissao)
            throw new BusinessException("A data de saída é inferior à data de emissão, favor ajustar a data de saída da nota.");

         // Desabilita o botão de cadastro caso o usuário não tenha acesso a tela responsável
         PerfilFuncionalidadeBusiness perfilFuncionalidadeBusiness = new PerfilFuncionalidadeBusiness();
         var usuario = PCInfo.Base.Core.Principal.UsuarioLogado;
         bool temPermissao = perfilFuncionalidadeBusiness.BuscarPorIdPerfil(usuario.idPerfil).Where(x => x.idFuncionalidade == (int)EnumMenu.NovaNFe).Any();
         if (!temPermissao)
            throw new BusinessException("Usuário logado não possui permissão para acessar esta funcionalidade.");

         ValidarCamposObrigatorioTransporte();
         if (cboFormaPagamento.SelectedValue != null && cboFormaPagamento.SelectedValue.ToInt32() == 1 && pagamentoObservable.Count == 0)
         {
            tbiPagamento.Focus();
            throw new BusinessException("É necessário adicionar pelo menos uma parcela para forma de pagamento a prazo.");
         }

         if (nfeProdutoObservable.Count == 0)
            throw new BusinessException("É necessário adicionar pelo menos um produto.");

         //iniciar captura de dados para salvar na tb_nfe
         if (nfe == null)
            nfe = new tb_nfe();

         if (nfe.DataCadastro.Year == 1)
            nfe.DataCadastro = DateTime.Now;
         nfe.Chave = txtChave.Text;
         nfe.DataEmissao = DateTime.Now;
         var hora = DateTime.Now.ToString("HH:mm:ss");
         nfe.DataEntradaSaida = (txtDataSaida.Text + " " + hora.ToString()).ToDateTime().AddMinutes(2);
         nfe.NumeroNota = txtNumeroNota.Text.ToIntOrNull();
         nfe.idLote = NumeroLoteCont;
         nfe.NumeroSerie = txtNumeroSerie.Text.ToIntOrNull();
         nfe.TipoEmissao = cboTipoEmissao.SelectedValue.ToInt32();
         nfe.FormaPagamento = cboFormaPagamento.SelectedValue.ToIntOrNull();

         nfe.CodigoFinalidade = cboFinalidade.SelectedValue.ToInt32();
         nfe.TipoNota = (EnumTipoNota)(cboTipoNota.SelectedValue.ToInt32());
         if (nfe.idNFStatus == null)
            nfe.idNFStatus = (int)EnumStatusNFe.AguardandoEnvio;

         nfe.NaturezaOperacao = StringUtis.RemoveCaracteresEspeciais(txtNaturezaOperacao.Text);
         nfe.Ambiente = cboAmbiente.SelectedValue.ToShort();

         nfe.InformacoesFisco = txtImpostoAproximado.Text;
         nfe.InformacoesEditaveis = txtInfEditaveis.Text;
         nfe.InformacoesComplementares = txtInfComplementaresPadrao.Text;

         nfe.idCliente = uscClienteDestinatario.Id.Value;
         nfe.idEmpresa = PCInfo.Base.Core.Principal.Empresa.id;
         nfe.ValorTotalDesconto = txtDescontoTotal.Text.ToStringReplaceMonetario().ToDecimalOrNull();

         if (nfe.idNfeOrigem == null || nfe.idNfeOrigem == 5)
         {
            nfe.idNfeOrigem = 5;
            txtOrigemNfe.Text = "Emissão Nfe";
         }

         if (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
            foreach (var nfeProduto in nfeProdutoObservable)
            {
               if (nfeProduto.ValorAliquotaICMS > 0)
                  nfeProduto.AliquotaCreditoICMS = nfeProduto.ValorAliquotaICMS;
               if (nfeProduto.ValorICMS > 0)
                  nfeProduto.ValorCreditoICMS = nfeProduto.ValorICMS;
            }

         nfe.tb_nfe_produto = nfeProdutoObservable;

         nfe.ChaveReferencia = txtchaveReferencia.Text;

         //preencher frete
         if (nfe.tb_nfe_frete == null) nfe.tb_nfe_frete = new tb_nfe_frete();
         nfe.tb_nfe_frete.idNFe = nfe.id;
         nfe.tb_nfe_frete.idTipoFrete = cboTipoFrete.SelectedValue.ToInt32();
         nfe.tb_nfe_frete.ValorFrete = txtValorFrete.Text.ToDecimalOrNull();
         nfe.tb_nfe_frete.QuantidadeVolumes = Convert.ToInt32(txtQuantidadeVolumesFrete.Text.ToDecimalOrNull().GetValueOrDefault());
         nfe.tb_nfe_frete.EspecieVolumes = (txtEspecieVolumes.Text != string.Empty) ? txtEspecieVolumes.Text : string.Empty;
         nfe.tb_nfe_frete.Marca = (txtMarca.Text != string.Empty) ? txtMarca.Text : string.Empty;
         nfe.tb_nfe_frete.Numeracao = (txtNumeracao.Text != string.Empty) ? txtNumeracao.Text : string.Empty;
         nfe.tb_nfe_frete.PesoBruto = (txtPesoBruto.Text != string.Empty) ? txtPesoBruto.Text.ToDecimal() : 0;
         nfe.tb_nfe_frete.PesoLiquido = (txtPesoLiquido.Text != string.Empty) ? txtPesoLiquido.Text.ToDecimal() : 0;
         nfe.tb_nfe_frete.idTransportadora = uscTransportadora.Id;
         nfe.tb_nfe_frete.idMotorista = uscMotorista.Id;
         nfe.tb_nfe_frete.idVeiculo = cboPlacaVeiculo.SelectedValue.ToIntOrNull();

         //Se for devolução preenche a tabela de referência NF-e
         if (cboTipoNota.SelectedValue.ToInt32() == (int)EnumTipoNota.DevolucaoCompra
           || cboTipoNota.SelectedValue.ToInt32() == (int)EnumTipoNota.DevolucaoVenda
           || devolucaoCompra != null || devolucaoVenda != null || cboFinalidade.SelectedValue.In(FinalidadeNFe.fnComplementar, FinalidadeNFe.fnAjuste))
         {
            nfe.tb_nfe_referencia = nfeReferenciaObservable;
            nfe.tb_nfe_referencia_cupom = nfeReferenciaCupomObservable;
         }
         else
         {
            nfe.tb_nfe_referencia_cupom = new List<tb_nfe_referencia_cupom>();
            nfe.tb_nfe_referencia = new List<tb_nfe_referencia>();
         }
         if (cboTipoNota.SelectedValue.ToInt32() == (int)EnumTipoNota.Entrada && cliente != null && cliente.ProdutorRural == true)
         {
            nfe.tb_nfe_referencia_contranota = nfeReferenciaContranotaObservable;
         }
         else
         {
            nfe.tb_nfe_referencia_contranota = new List<tb_nfe_referencia_contranota>();
         }
         //Se todos os CFOPS forem 5929 ou 6929(Lançamento relativo a Cupom Fiscal) preenche a tabela de referência cupom
         //if (!nfeProdutoObservable.Where(x => x.idCFOP != 5929 && x.idCFOP != 6929).Any())
         //   nfe.tb_nfe_referencia_cupom = nfeReferenciaCupomObservable;
         //else
         //   nfe.tb_nfe_referencia_cupom = new List<tb_nfe_referencia_cupom>();

         //Grava na tabela tb_nfe_pagamento os dados do combo de forma de pagamento
         if (parametro.tb_nf_configuracao.Versao == false)
         {
            if (nfe.FormaPagamento != null)
            {
               nfe.FormaPagamento = nfe.FormaPagamento;
               nfe.tb_nfe_pagamento = pagamentoObservable;
               formaPagamentoObservable.Clear();
               nfe.tb_nfe_formapagamento = formaPagamentoObservable;
            }
         }
         else if (parametro.tb_nf_configuracao.Versao == true)
         {
            //Grava na tabela tb_nfe_formaPagamento os dados do combo de forma de pagamento versão 4.0
            if (nfe.FormaPagamento != null && (nfe.FormaDePagamento != null || formaPagamentoObservable.Count > 0))
            {
               //grava na tabela tb_nfe os dados do combo de forma de pagamento dentro do campo forma de pagamento
               nfe.FormaPagamento = nfe.FormaPagamento;
               nfe.tb_nfe_pagamento = pagamentoObservable;
               nfe.tb_nfe_formapagamento = formaPagamentoObservable;
            }
         }
         nfe.Versao = "4.00";

         if (compra != null)
            nfeBusiness.CriarNovaNota(nfe, compra);
         else if (venda != null)
            nfeBusiness.CriarNovaNota(nfe, venda);
         else if (devolucaoCompra != null)
            nfeBusiness.CriarNovaNota(nfe, devolucaoCompra);
         else if (devolucaoVenda != null)
            nfeBusiness.CriarNovaNota(nfe, devolucaoVenda);
         else if (ordemServico != null)
            nfeBusiness.CriarNovaNota(nfe, ordemServico);
         //else if (listaCupons != null)
         //nfeBusiness.CriarNovaNota(nfe, listaCupons);
         else
         {
            nfeBusiness.Salvar(nfe);
            nfe = nfeBusiness.BuscarPorId(nfe.id);
            nfe.tb_nfe_formapagamento = new NFeFormaPagamentoBusiness().BuscarPorIdNFe(nfe.id);
         }

         nfeBusiness.GerarNFe4_0(nfe, false, true);
         MessageBoxUtils.ExibeMensagemSucesso();
         tbiTotais.IsEnabled = true;
         txtChave.Text = nfe.Chave;
         CalcularTotais(nfe);
         btnVisualizarDanfe.IsEnabled = true;
         btnNovaNfe.IsEnabled = true;
         btnTransmitir.IsEnabled = true;
         //recuperar dados para inserir no grid de produtos
         dgProdutos.ItemsSource = null;
         nfeProdutoObservable = new ObservableCollection<tb_nfe_produto>();
         int contador = 1;
         List<tb_produto> produtoList = new List<tb_produto>();
         foreach (var item in nfe.tb_nfe_produto)
         {
            var produto = new ProdutoBusiness().BuscarPorId(item.idProduto);
            var buscaUnid = new UnidadeBusiness().BuscarPorId(produto.idUnidade);

            produto.tb_unidade = buscaUnid;
            produtoList.Add(produto);
            item.tb_produto = produto;
            item.NumeroItem = contador;
            contador++;
            nfeProdutoObservable.Add(item);
         }
         dgProdutos.ItemsSource = nfeProdutoObservable;
         ///
      }
      #endregion

      #region Visualizar Danfe
      private void VisualizarDANFE(tb_nfe nfe)
      {
         var sVersao = "4.00";
         frmVisualizacaoDANFE reldanfe = null;
         if (idTipoPag == null)
            idTipoPag = cboFormaPagamento.SelectedValue.ToInt32();
         if (this.nfe != null)
            reldanfe = new frmVisualizacaoDANFE(nfe.XML, nfe.XMLRetorno, (TipoAmbiente)nfe.Ambiente, sVersao, idTipoPag);
         else
            MessageBox.Show("É necessário Salvar a nota para visualizar a Danfe!");

         if (reldanfe != null)
            reldanfe.ShowDialog();
      }

      private void VisualizarDanfeFS(tb_nfe nfe)
      {
         frmVisualizacaoDanfeFS reldanfe = new frmVisualizacaoDanfeFS(nfe.XML, nfe.XMLRetorno, nfe.ChaveFS);
         reldanfe.ShowDialog();
      }


      private void btnVisualizarDanfe_Click(object sender, RoutedEventArgs e)
      {
         if (nfe != null)
            VisualizarDANFE(nfe);
      }
      #endregion

      #region NovaNfe
      private void btnNovaNfe_Click(object sender, RoutedEventArgs e)
      {
         if (MessageBoxUtils.ExibeMensagemQuestion("Deseja Criar nova NF-e?"))
         {
            Utilidades.LimparControles(tbcGeral);
            nfeProdutoObservable.Clear();
            nfeReferenciaContranotaObservable.Clear();
            Utilidades.LimparControles(gbxImpostoAproximado);
            Utilidades.LimparControles(gbxInformacaoPadrao);
            txtChave.Text = string.Empty;
            txtNaturezaOperacao.Text = string.Empty;

            InitializeComponent();
            ConfiguracoesIniciais();
            txtStatus.Text = EnumStatusNFe.AguardandoEnvio.ToString();
            txtStatus.Foreground = AlterarCorStatus(txtStatus.Text);
            btnSalvar.IsEnabled = true;
            tbiCabecalho.Focus();
         }
         else { e.Handled = true; ; }
      }
      #endregion

      #region Transmitir NFe

      private void btnTransmitir_Click(object sender, RoutedEventArgs e)
      {
         SalvarNota();

         ValidarImpostosProdutos();
         if (!string.IsNullOrEmpty(cliente.Email) && !Validator.ValidarEmail(cliente.Email))
            throw new BusinessException("Email do cliente inválido.");
         try
         {
            // Colocar thread no método para não travar o sistema
            // metodo que efetua a geração do xml conforme a versão selecionada (false 3.10 e true 4.0)
            nfeBusiness.GerarNFe4_0(nfe, true);

            var cliente = new ClienteBusiness().BuscarPorId(nfe.idCliente);
            if (nfe.TipoEmissao != (int)TipoEmissao.teFSDA && nfe.TipoEmissao != (int)TipoEmissao.teFSIA)
            {
               PreencherCampos();
               if (!string.IsNullOrEmpty(cliente.Email))
               {
                  MessageBoxUtils.ExibeMensagem("Nota Fiscal " + nfe.NumeroNota + " enviada com sucesso!");
                  EnviarEmail(cliente, nfe);
                  tbiTotais.Visibility = Visibility.Visible;
               }
               else
               {
                  MessageBoxUtils.ExibeMensagem("Nota Fiscal " + nfe.NumeroNota + " enviada com sucesso!\nO cliente não possui e-mail cadastrado para envio.");
                  tbiTotais.Visibility = Visibility.Visible;
               }
            }
            else
            {
               PreencherCampos();

               if (!string.IsNullOrEmpty(cliente.Email))
               {
                  MessageBoxUtils.ExibeMensagem("Nota Fiscal " + nfe.NumeroNota + " salva em formulário de segurança!");
                  EnviarEmail(cliente, nfe);
               }
               else
                  MessageBoxUtils.ExibeMensagem("Nota Fiscal " + nfe.NumeroNota + " salva em formulário de segurança!\nNão foi possível enviar e-mail para o cliente. E-mail não cadastrado.");

               VisualizarDanfeFS(nfe);
            }
         }
         catch (NFeException nfx)
         {
            PreencherCampos();
            PreencherErros();
            MessageBoxUtils.ExibeMensagemAdvertencia(nfx.mensagem);


            var listaErros = new NFErrosBusiness().BuscarPorIdNF(nfe.id);
            if (listaErros != null && listaErros.Any() && listaErros.FirstOrDefault().Codigo == 225)
            {
               var frmConsultaErrosNFeOnline = new frmConsultaErrosNFeOnline(nfe);
               frmConsultaErrosNFeOnline.Show();
            }
            else if (listaErros != null && listaErros.Any() && listaErros.FirstOrDefault().Codigo == 778)
            {
               var erro = listaErros.FirstOrDefault();
               var numeroItem = erro.Mensagem.Replace("Rejeicao: Informado NCM inexistente - [nItem: ", "").Replace("]", "").ToInt32();
               List<tb_nfe_produto> nfeprod = new List<tb_nfe_produto>();
               foreach (var item in nfeProdutoObservable)
               {
                  item.tb_produto = new ProdutoBusiness().BuscarPorId(item.idProduto);
                  nfeprod.Add(item);
               }

               var nfeProduto = nfeProdutoObservable.Where(x => x.NumeroItem == numeroItem).FirstOrDefault();
               var produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);
               if (MessageBoxUtils.ExibeMensagemQuestion(string.Format("NCM Inválido para o Produto {0}.\nDeseja consultar Online?", produto.Descricao)))
               {
                  dgNFErros.SelectedItem = dgNFErros.Items[0];
                  //btnConsultarNCM_Click(null, null);
               }
            }
            else if (listaErros != null && listaErros.Any() && listaErros.FirstOrDefault().Codigo == 806)
            {
               var erro = listaErros.FirstOrDefault();
               if (erro.Codigo == 806)
               {
                  var listaSemCEST = nfe.tb_nfe_produto.Where(x => x.tb_produto.CEST == null).ToList();
                  bool excluir = true;
                  foreach (var nfeProduto in listaSemCEST)
                  {
                     if (MessageBoxUtils.ExibeMensagemQuestion(string.Format("Produto {0} não possui CEST informado, deseja inserir agora?", nfeProduto.tb_produto.Descricao)))
                     {
                        var produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);
                        var listaCEST = new PCInfo.Utils.ConsultaCEST().ConsultaCestPorNCM(produto.NCM);
                        frmSelecionarCEST frm = new frmSelecionarCEST(listaCEST);
                        if (frm.ShowDialog().Value)
                        {
                           var cestSelecionado = frm.CESTSelecionado.Replace(".", "").ToString().Trim();
                           produtoBusiness.AtualizarCEST(produto.id, cestSelecionado);
                           if (excluir)
                           {
                              new NFErrosBusiness().Excluir(erro);
                              excluir = false;
                           }
                           MessageBoxUtils.ExibeMensagemSucesso("CEST Atualizado com Sucesso!");
                        }
                     }
                  }

               }
            }
            else if (listaErros != null && listaErros.Any() && listaErros.FirstOrDefault().Codigo == 204)
            {
               if (MessageBoxUtils.ExibeMensagemQuestion("Chave em Duplicidade!\rFavor realizar a consulta online na tela 'Consulta NF-e'.\rDeseja consultar agora ?"))
               {
                  EnviarParametro("ConsultaNFe", nfe, true);
                  //this.Close
                  return;
               }
            }
            //recuperar dados para inserir no grid de produtos
            dgProdutos.ItemsSource = null;
            nfeProdutoObservable = new ObservableCollection<tb_nfe_produto>();
            int contador1 = 1;
            List<tb_produto> produtoList1 = new List<tb_produto>();
            foreach (var item in nfe.tb_nfe_produto)
            {
               var produto = new ProdutoBusiness().BuscarPorId(item.idProduto);
               var buscaUnid = new UnidadeBusiness().BuscarPorId(produto.idUnidade);

               produto.tb_unidade = buscaUnid;
               produtoList1.Add(produto);
               item.tb_produto = produto;
               item.NumeroItem = contador1;
               contador1++;
               nfeProdutoObservable.Add(item);
            }

            dgProdutos.ItemsSource = nfeProdutoObservable;
            ///
            return;
         }
         catch (BusinessException ex)
         {
            PCInfo.Utils.Log.Instancia().GravarErro(ex);
            if (ex.mensagem.Contains("Erro ao converter o retorno do xml. Favor entrar em contato com o Suporte Técnico.") && cboTipoEmissao.SelectedValue.ToInt32() != (int)TipoEmissao.teFSDA)
            {
               if (MessageBoxUtils.ExibeMensagemQuestion("Portal da Nota Fiscal Eletrônica ou conexão de internet do cliente indisponível, para emissão da nota fiscal eletrônica o cliente poderá utilizar a emissão em contingência. Lembrando que, caso o cliente esteja sem internet a única opção de emissão da NFe é por contingência em formulário de segurança, modelo: ContingênciaFS.\nDeseja emitir a nota em contingência?"))
               {
                  ////PreencherCampos();
                  //tabCabecalho.Focus();
                  //cboTipoEmissao.Focus();
                  //cboTipoEmissao.IsDropDownOpen = true;
                  return;
               }
            }
            else if (ex != null && ex.mensagem != null)
            {
               PreencherCampos();
               //tabCabecalho.Focus();
               MessageBoxUtils.ExibeMensagemAdvertencia(ex.mensagem);
            }
            else
               throw;
         }
         catch (WebException ex)
         {
            PCInfo.Utils.Log.Instancia().GravarErro(ex);
            //PreencherCampos();
            //tabCabecalho.Focus();
            if (ex.Message.ToLower().Contains("não foi possível criar um canal seguro para ssl/tls"))
            {
               MessageBoxUtils.ExibeMensagem("Por favor verifique se o certificado é válido.");
               PreencherCampos();
               // tabCabecalho.Focus();
            }
            else
            {
               if (cboAmbiente.SelectedValue.ToShort() == 1)
               {
                  if (MessageBoxUtils.ExibeMensagemQuestion("Não foi possível validar o retorno do XML pois a conexão foi perdida.\rAntes de emitir novamente, favor realizar a consulta na tela 'Consulta NF-e'.\rDeseja consultar agora ?"))
                  {
                     EnviarParametro("ConsultaNFe", nfe, true);
                     //this.Close();
                     return;
                  }
                  else
                  {
                     PreencherCampos();
                     //tabCabecalho.Focus();
                  }
               }
               else
                  MessageBoxUtils.ExibeMensagemAdvertencia("Não foi possível validar o retorno do XML pois a conexão foi perdida.");
            }
         }
         catch (Exception ex)
         {
            MessageBoxUtils.ExibeMensagemAdvertencia("Ocorreu Problemas de Comunicação com o Servidor! " + ex.Message);
            PreencherCampos();
            // tabCabecalho.Focus();
         }

         //recuperar dados para inserir no grid de produtos
         dgProdutos.ItemsSource = null;
         nfeProdutoObservable = new ObservableCollection<tb_nfe_produto>();
         int contador = 1;
         List<tb_produto> produtoList = new List<tb_produto>();
         foreach (var item in nfe.tb_nfe_produto)
         {
            var produto = new ProdutoBusiness().BuscarPorId(item.idProduto);
            var buscaUnid = new UnidadeBusiness().BuscarPorId(produto.idUnidade);

            produto.tb_unidade = buscaUnid;
            produtoList.Add(produto);
            item.tb_produto = produto;
            item.NumeroItem = contador;
            contador++;
            nfeProdutoObservable.Add(item);
         }

         dgProdutos.ItemsSource = nfeProdutoObservable;
         ///
      }
      private void ValidarImpostosProdutos()
      {
         var produtoBusiness = new ProdutoBusiness();
         foreach (var nfeProduto in nfeProdutoObservable)
         {
            var produto = produtoBusiness.BuscarPorId(nfeProduto.idProduto);

            //ICMS CONTRIBUINTE
            if (!nfeProduto.idCSOSN.HasValue && parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)
            {
               if ((cliente.ConsumidorFinal && (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)) && (nfeProduto.idCSOSN.In(101, 102, 103, 201, 202, 203, 300, 400, 900)))
                  throw new BusinessException(String.Format("Produto {0} possui CSOSN incompatível para consumidor final.", produto.Descricao));
            }
            else if (parametro.idRegimeTributario != (int)EnumTipoEmpresa.SimplesNacional)
            {
               if ((cliente.ConsumidorFinal && (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)) && (nfeProduto.idCSTICMS.In(10, 70, 90)))
                  throw new BusinessException(String.Format("Produto {0} possui CST ICMS incompatível para consumidor final.", produto.Descricao));
            }
            //PIS CONTRIBUINTE
            if ((cliente.ConsumidorFinal && (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)) && (nfeProduto.idCSTPIS != null && nfeProduto.idCSTPIS != 4 && nfeProduto.idCSTPIS != 6 && nfeProduto.idCSTPIS != 7 && nfeProduto.idCSTPIS != 8 && nfeProduto.idCSTPIS != 70 && nfeProduto.idCSTPIS != 71 && nfeProduto.idCSTPIS != 72 && nfeProduto.idCSTPIS != 73 && nfeProduto.idCSTPIS != 74 && nfeProduto.idCSTPIS != 48 && nfeProduto.idCSTPIS != 98 && nfeProduto.idCSTPIS != 99))
               throw new BusinessException(String.Format("Produto {0} possui CST PIS incompatível para consumidor final.", produto.Descricao));

            //COFINS CONTRIBUINTE
            if ((cliente.ConsumidorFinal && (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)) && (nfeProduto.idCSTCOFINS != null && nfeProduto.idCSTCOFINS != 4 && nfeProduto.idCSTCOFINS != 6 && nfeProduto.idCSTCOFINS != 7 && nfeProduto.idCSTCOFINS != 8 && nfeProduto.idCSTCOFINS != 70 && nfeProduto.idCSTCOFINS != 71 && nfeProduto.idCSTCOFINS != 72 && nfeProduto.idCSTCOFINS != 73 && nfeProduto.idCSTCOFINS != 74 && nfeProduto.idCSTCOFINS != 48 && nfeProduto.idCSTCOFINS != 98 && nfeProduto.idCSTCOFINS != 99))
               throw new BusinessException(String.Format("Produto {0} possui CST COFINS incompatível para consumidor final.", produto.Descricao));

            //IPI CONTRIBUINTE
            if ((cliente.ConsumidorFinal && (parametro.idRegimeTributario == (int)EnumTipoEmpresa.SimplesNacional)) && parametro.Industrial && (nfeProduto.idCSTIPI != null && nfeProduto.idCSTIPI != 1 && nfeProduto.idCSTIPI != 2 && nfeProduto.idCSTIPI != 3 && nfeProduto.idCSTIPI != 4 && nfeProduto.idCSTIPI != 5 && nfeProduto.idCSTIPI != 51 && nfeProduto.idCSTIPI != 52 && nfeProduto.idCSTIPI != 53 && nfeProduto.idCSTIPI != 54 && nfeProduto.idCSTIPI != 99))
               throw new BusinessException(String.Format("Produto {0} possui CST IPI incompatível para consumidor final.", produto.Descricao));
         }
      }
      #endregion

      #region Declaração de importação

      private void CarregarTpIntermedio()
      {
         cboFormaImportacao.ItemsSource = EnumHelper.ToList(typeof(TipoIntermediacao));
         cboFormaImportacao.DisplayMemberPath = "Value";
         cboFormaImportacao.SelectedValuePath = "Key";
      }

      private void CarregarTpViaTransf()
      {
         cboTipoViaTransporte.ItemsSource = EnumHelper.ToList(typeof(TipoTransporteInternacional));
         cboTipoViaTransporte.DisplayMemberPath = "Value";
         cboTipoViaTransporte.SelectedValuePath = "Key";
      }

      private void btnAdicionarAdicao_Click(object sender, RoutedEventArgs e)
      {
         frmAdicaoDeclaracaoImportacao frm = new frmAdicaoDeclaracaoImportacao();
         frm.ShowDialog();
         if (frm.Adicao != null)
         {
            frm.Adicao.NumeroSequencialAdicao = adicaoObservable.Count + 1;
            adicaoObservable.Add(frm.Adicao);

            dgAdicao.ItemsSource = null;
            dgAdicao.ItemsSource = adicaoObservable;
         }
      }
      private void PreencherCamposDeclaracaoImportacao(tb_nfe_produto produtoSelecionado)
      {
         if (NotaImportacao())
         {
            tbiImpostoImportacao.Visibility = Visibility.Visible;
            tbiDeclaracaoImportacao.Visibility = Visibility.Visible;

            var declaracao = produtoSelecionado.tb_nfe_declaracao_importacao.FirstOrDefault();
            if (declaracao != null)
            {
               txtNumeroDI.Text = declaracao.NumeroDI;
               txtDataDelacarao.Text = declaracao.DataRegistroDI.ToShortDateString();
               txtLocalDesembarque.Text = declaracao.LocalDesembarque;
               cboUFDesembarque.Text = declaracao.UFDesembarque;
               txtDataDesembarque.Text = declaracao.DataOcorrenciaDesembarque.ToShortDateString();
               cboTipoViaTransporte.SelectedValue = (TipoTransporteInternacional)declaracao.TipoViaTransporte;
               txtValorAFRMM.Text = declaracao.ValorAFRMM.ToStringOrNull();
               cboFormaImportacao.SelectedValue = (TipoIntermediacao)declaracao.FormaImportacao;
               txtCNPJAdquirente.Text = declaracao.CNPJAdquirente;
               cboUFAdquirente.Text = declaracao.UFAdquirente;
               txtCodigoExportador.Text = declaracao.CodigoExportador;

               dgAdicao.ItemsSource = null;
               adicaoObservable.Clear();
               declaracao.tb_nfe_declaracao_importacao_adicao.ToList().ForEach(x => adicaoObservable.Add(x));

               dgAdicao.ItemsSource = adicaoObservable;

               //Imposto Importação
               txtBaseCalculoII.Text = produtoSelecionado.ValorBCII.ToStringOrNull();
               txtValorII.Text = produtoSelecionado.ValorII.ToStringOrNull();
               txtDespesasAduaneiras.Text = produtoSelecionado.ValorDespesasAduaneiras.ToStringOrNull();
               txtValorIOF.Text = produtoSelecionado.ValorIOF.ToStringOrNull();
            }
            else
            {
               txtNumeroDI.Text = string.Empty;
               txtDataDelacarao.Text = string.Empty;
               txtLocalDesembarque.Text = string.Empty;
               cboUFDesembarque.Text = string.Empty;
               txtDataDesembarque.Text = string.Empty;
               cboTipoViaTransporte.Text = string.Empty;
               txtValorAFRMM.Text = string.Empty;
               cboFormaImportacao.Text = string.Empty;
               txtCNPJAdquirente.Text = string.Empty;
               cboUFAdquirente.Text = string.Empty;
               txtCodigoExportador.Text = string.Empty;

               dgAdicao.ItemsSource = null;

               //Imposto Importação
               txtBaseCalculoII.Text = null;
               txtValorII.Text = null;
               txtDespesasAduaneiras.Text = null;
               txtValorIOF.Text = null;
            }
         }
      }
      private void ObterDadosImportacao(tb_nfe_produto nfeProduto)
      {
         if (NotaImportacao())
         {
            tb_nfe_declaracao_importacao declaracao = null;
            if (nfeProduto.tb_nfe_declaracao_importacao == null || nfeProduto.tb_nfe_declaracao_importacao.Count == 0)
               declaracao = new tb_nfe_declaracao_importacao();
            else
               declaracao = nfeProduto.tb_nfe_declaracao_importacao.FirstOrDefault();

            declaracao.idNFeProduto = nfe.id;
            declaracao.NumeroDI = txtNumeroDI.Text;
            declaracao.DataRegistroDI = txtDataDelacarao.Text.ToDateTime();
            declaracao.LocalDesembarque = txtLocalDesembarque.Text;
            declaracao.UFDesembarque = cboUFDesembarque.Text;
            declaracao.DataOcorrenciaDesembarque = txtDataDesembarque.Text.ToDateTime();
            declaracao.TipoViaTransporte = (int)cboTipoViaTransporte.SelectedValue;
            declaracao.ValorAFRMM = txtValorAFRMM.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            declaracao.FormaImportacao = (int)cboFormaImportacao.SelectedValue;
            declaracao.CNPJAdquirente = txtCNPJAdquirente.Text.LimparTexto();
            declaracao.UFAdquirente = cboUFAdquirente.Text;
            declaracao.CodigoExportador = txtCodigoExportador.Text;

            if (adicaoObservable.Count > 0)
               declaracao.tb_nfe_declaracao_importacao_adicao = adicaoObservable.ToList();


            nfeProduto.tb_nfe_declaracao_importacao.Clear();
            nfeProduto.tb_nfe_declaracao_importacao.Add(declaracao);

            //Imposto Importação
            nfeProduto.ValorBCII = txtBaseCalculoII.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.ValorII = txtValorII.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.ValorDespesasAduaneiras = txtDespesasAduaneiras.Text.ToStringReplaceMonetario().ToDecimalOrNull();
            nfeProduto.ValorIOF = txtValorIOF.Text.ToStringReplaceMonetario().ToDecimalOrNull();
         }
         else
         {
            nfeProduto.tb_nfe_declaracao_importacao = null;
            nfeProduto.ValorBCII = null;
            nfeProduto.ValorII = null;
            nfeProduto.ValorDespesasAduaneiras = null;
            nfeProduto.ValorIOF = null;
         }
      }


      private void dgAdicao_PreviewKeyDown(object sender, KeyEventArgs e)
      {
         if (nfe != null &&
              (nfe.idNFStatus != (int)EnumStatusNFe.Cancelada &&
              nfe.idNFStatus != (int)EnumStatusNFe.EmitidaComSucesso &&
              nfe.idNFStatus != (int)EnumStatusNFe.Inutilizada &&
              nfe.idNFStatus != (int)EnumStatusNFe.CanceladaSemEmissao))
         {
            var adicao = dgAdicao.SelectedItem as tb_nfe_declaracao_importacao_adicao;
            if (adicao != null)
            {
               if (e.Key == Key.Delete)
               {
                  if (MessageBoxUtils.ExibeMensagemQuestionExcluirRegistros())
                  {
                     adicaoObservable.Remove(adicao);

                     for (int i = 0; i < adicaoObservable.Count; i++)
                        adicaoObservable[i].NumeroAdicao = i + 1;

                     dgAdicao.ItemsSource = null;
                     dgAdicao.ItemsSource = adicaoObservable;
                  }
                  else { e.Handled = true; };
               }
            }
         }
      }

      private void btnExcluirAdicao_Click(object sender, RoutedEventArgs e)
      {
         var adicaoSelecionada = dgAdicao.SelectedItem as tb_nfe_declaracao_importacao_adicao;

         if (adicaoSelecionada != null && MessageBoxUtils.ExibeMensagemQuestionExcluirRegistros())
         {
            adicaoObservable.Remove(adicaoSelecionada);
         }
         else { e.Handled = true; };
      }
      private void dgAdicao_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         var adicaoSelecionada = dgAdicao.SelectedItem as tb_nfe_declaracao_importacao_adicao;
         if (adicaoSelecionada != null)
            btnExcluirAdicao.IsEnabled = true;
         else
            btnExcluirAdicao.IsEnabled = false;
      }


      #endregion

      #region Enviar Email
      private void EnviarEmail(tb_cliente cliente, tb_nfe nfe)
      {
         var parametro = new ParametroBusiness().BuscarParametroVigente();
         var configuracao = parametro.tb_parametro_configuracao;
         if (MessageBoxUtils.ExibeMensagemQuestion("Deseja enviar email para o cliente?"))
         {
            if (cliente.Email == string.Empty || cliente.Email == null)
            {
               MessageBoxUtils.ExibeMensagem("Favor inserir um e-mail para o cliente.");
               return;
            }
            var nomeArquivo = nfe.Chave + ".pdf";
            Email email = new Email();

            if (new ParametroBusiness().BuscarParametroVigente().EnviarEmailSmtpImap && configuracao != null)
            {
               email.ServidorSMTP = configuracao.SMTP_IMAP;
               email.PortaSMTP = configuracao.PortaSMTP;
               email.Senha = Criptografia.Descriptografar(configuracao.Senha);
               email.Remetente = configuracao.Usuario;
               email.HabilitarSSL = configuracao.HabilitarSSL;
            }
            else
            {
               email.ServidorSMTP = PCInfo.Plus.Utils.Constantes.EMAIL_PADRAO_SERVIDOR;
               email.PortaSMTP = PCInfo.Plus.Utils.Constantes.EMAIL_PADRAO_PORTA;
               email.Senha = Criptografia.Descriptografar(PCInfo.Plus.Utils.Constantes.EMAIL_PADRAO_SENHA_CRIPTOGRAFADA);
               email.Remetente = PCInfo.Plus.Utils.Constantes.EMAIL_PADRAO_USUARIO;
               email.HabilitarSSL = false;
            }

            email.NomeRemetente = cliente.Nome;
            email.Assunto = "Nota Fiscal Eletrônica " + (PCInfo.Base.Core.Principal.Empresa as tb_empresa).RazaoSocial;
            if (parametro.tb_parametro_opcao != null && !string.IsNullOrEmpty(parametro.tb_parametro_opcao.MensagemPadraoNFe))
               email.Conteudo = nfeBusiness.ConfigurarMensagemPadrao(parametro.tb_parametro_opcao.MensagemPadraoNFe, nfe);
            else
               email.Conteudo = "Referente à NFe " + nfe.Chave;

            //Adiciona o e-mail principal
            email.Destinatarios.Add(cliente.Email);
            //Adiciona todos os e-mails 
            var listaEmails = new ClienteEmailBusiness().BuscarPorIdCliente(cliente.id);
            if (listaEmails != null && listaEmails.Count > 0)
               foreach (var item in listaEmails)
                  email.Destinatarios.Add(item.Email);

            var frmVisualizacaoDANFE = new frmVisualizacaoDANFE(nfe.XML, nfe.XMLRetorno, (TipoAmbiente)nfe.Ambiente, "4.00");

            using (var memoryStream = new RelatorioUtils().ReportToPDF(frmVisualizacaoDANFE._reportViewer))
            {
               System.Net.Mime.ContentType ct = new System.Net.Mime.ContentType(System.Net.Mime.MediaTypeNames.Application.Pdf);
               System.Net.Mail.Attachment attach = new System.Net.Mail.Attachment(memoryStream, ct);
               attach.ContentDisposition.FileName = nomeArquivo;

               email.Anexo.Add(attach);
               var nomePasta = (nfe.DataEmissao.Year + "-" + nfe.DataEmissao.Month.ToString().PadLeft(2, '0')).ToString();

               if (!System.IO.File.Exists(new ParametroBusiness().BuscarParametroVigente().tb_nf_configuracao.CaminhoArquivo + "\\" + nomePasta + "\\" + nfe.Chave + "-procNfe.xml"))
                  throw new BusinessException("Arquivo inexistente no diretório.");

               email.Anexo.Add(new Attachment(new ParametroBusiness().BuscarParametroVigente().tb_nf_configuracao.CaminhoArquivo + "\\" + nomePasta + "\\" + nfe.Chave + "-procNfe.xml"));
               try
               {
                  new EmailUtils().Enviar(email);
                  MessageBoxUtils.ExibeMensagem("E-mail enviado com sucesso!");
               }
               catch (Exception ex)
               {
                  MessageBoxUtils.ExibeMensagem("Ocorreu erro ao enviar E-mail.\n" + ex.Message);
               }
               finally
               {
                  frmVisualizacaoDANFE.Close();
               }
            }
         }
      }


      #endregion

      #region Carta de Correção
      private void CarregarCartasCorrecao()
      {
         var listaCartas = new NFCorrecaoBusiness().BuscarPorIdNF(nfe.id).OrderBy(x => x.DataEnvio);
         if (listaCartas != null && listaCartas.Count() > 0)
         {
            dgCartas.ItemsSource = null;
            dgCartas.ItemsSource = listaCartas.ToList();
            tbiCartaCorrecao.Visibility = Visibility.Visible;
         }
         else
         {
            dgCartas.ItemsSource = null;
            tbiCartaCorrecao.Visibility = Visibility.Collapsed;
         }
      }
      #endregion

   }
}
