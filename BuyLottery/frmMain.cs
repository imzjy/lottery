using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using System.Net;
using System.Diagnostics;
using BuyLottery.DataAccess;
using BuyLottery.Common;

namespace BuyLottery
{
    public partial class frmMain : Form
    {
        public enum BrowserType
        { 
            Login = 0,
            VisitBuy = 1,
            Buy = 2,
            None = 3
        }

        public frmMain()
        {
            InitializeComponent();
        }

        Queue<TicketsQueueItem> ticketQueue = null;
        static string caipiao_main_url = "http://caipiao.taobao.com/lottery/order/united_hall.htm?lottery_type=SSQ";
        static string caipiao_login_url = "https://login.taobao.com/member/login.jhtml?f=top&redirectURL=http%3A%2F%2Fcaipiao.taobao.com%2F";
        static string page_url_template = "http://caipiao.taobao.com/lottery/order/united_list.htm?page={0}&lottery_type=SSQ";
        static int totalTicketsNumber = 0;
        static DataTable dtTickets = null;
        static DataView dvTickets = null;
        static BrowserType browser_type = BrowserType.Login;
        
        private void Form1_Load(object sender, EventArgs e)
        {
            
            browser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(browser_DocumentCompleted);

            ShowUserMessage("正在打开淘宝彩票......请稍等");
            browser_type = BrowserType.Login;
            browser.Navigate(caipiao_login_url);
        }

        void browser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            //if (e.Url.ToString() == "about:blank")
            //{
            //    return;
            //}

            //if (browser_type == BrowserType.Login)
            //{
            //    this.toolStripStatusMessage.Text = "请先登录淘宝彩票";
            //}

            //if (browser_type == BrowserType.VisitBuy)
            //{
            //    browser_type = BrowserType.Buy;
            //    var btn = browser.Document.GetElementById("J-confirm-pay");
            //    if (btn != null)
            //        btn.InvokeMember("click");
            //}

            //if (browser_type == BrowserType.Buy)
            //{
            //    //var ticketUrl = ticketQueue.Dequeue();
            //    //browser.Navigate(ticketUrl);
            //    browser_type = BrowserType.None;
            //}
        }

        private static int GetTotalPageNumbers()
        {
            WebClient web = new WebClient();
            string hall_main_html = web.DownloadString(caipiao_main_url);

            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(hall_main_html);

            //get total pages
            var totalPagesXPath = "//*[@id=\"listpage\"]/div/span[2]/span/a[5]";
            var totalPages = doc.DocumentNode.SelectSingleNode(totalPagesXPath).InnerText;
            int num = 0, nPages = 0;
            if (int.TryParse(totalPages, out num))
                nPages = num;
            else
                nPages = 0;
            return nPages;
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            gvTickets.AutoGenerateColumns = false;
            dtTickets = DAHelper.GetTickets();

            dvTickets = new DataView(dtTickets);
            this.BeginInvoke(new Action<int>(UIUpdateStatusTotalRecords), dvTickets.Count);
            gvTickets.DataSource = dvTickets;

            ShowUserMessage("数据库更新完成!");
        }

        void DoRetriveTickets(object sender, DoWorkEventArgs e)
        {
#if DEBUG
            int nPages = 1;
#else
            
            int nPages = GetTotalPageNumbers();
#endif

            for (int i = 0; i < nPages; i++)
            {
                var pageUrl = string.Format(page_url_template, i + 1);
                Debug.WriteLine(pageUrl);
                this.BeginInvoke(new Action<string>(UIUpdateStatusMessage), string.Format("retrive from {0}", pageUrl));
                GetTickets(pageUrl);
            }
        }

        private void GetTickets(string pageUrl)
        {
            HttpWebRequest web = (HttpWebRequest)HttpWebRequest.Create(pageUrl);
            var response = web.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding(936));
            var page_html = sr.ReadToEnd();

            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(page_html);

            var tBodyXPath = "//*[@id=\"J-BuyList\"]/tbody";
            var tbody = doc.DocumentNode.SelectSingleNode(tBodyXPath);
            var rows = tbody.SelectNodes("tr");

            for (int i = 0; i < rows.Count; i++)  
            {
                var row = rows[i];
                var cols = row.SelectNodes("td");

                var seq         = cols[0].InnerText;
                var creator     = cols[1].InnerText.Trim();
                var title       = cols[2].InnerText.Trim();
                var amount      = decimal.Parse(cols[3].InnerText);
                var price       = decimal.Parse(cols[4].InnerText);
                var progress    = decimal.Parse(cols[5].InnerText.TrimEnd('%'))/100;
                var url         = cols[2].SelectSingleNode("a").Attributes["href"].Value;
                var id          = cols[6].SelectSingleNode("a").Attributes["rel"].Value;
                DAHelper.AddTickets(id, creator, title, amount, price, progress, url, TicketStatus.New);

                totalTicketsNumber += 1;
                this.BeginInvoke(new Action<int>(UIUpdateStatusTotalRecords), totalTicketsNumber);
            }
        }

        private void UIUpdateStatusTotalRecords(int curTicketNumbers)
        {
            ShowTotalRecords(curTicketNumbers);
        }
        private void UIUpdateStatusMessage(string info)
        {
            ShowUserMessage(info);
        }
        private void ShowTotalRecords(int nTotalCount)
        {
            string totalCount = String.Format("记录总数: {0}", nTotalCount);
            toolStripStatusTotalRecord.Text = totalCount;
        }
        private void ShowUserMessage(string msg)
        {
            string message = string.Format("提示信息: {0}", msg);
            toolStripStatusMessage.Text = message;
        }

        private void btnBuy_Click(object sender, EventArgs e)
        {
            if (dvTickets == null || dvTickets.Count <= 0)
            {
                toolStripStatusMessage.Text = "当前购买列表为空，请更新数据库，或重新在本地筛选。";
                return;
            }

            tabControl1.SelectedIndex = 0;

            if (btnBuy.Text == "购买")
            {
                ticketQueue = new Queue<TicketsQueueItem>();
                DataTable dtTicketToBuy = dvTickets.ToTable();
                foreach (DataRow row in dtTicketToBuy.Rows)
                {
                    TicketsQueueItem item = new TicketsQueueItem(row["id"].ToString(), row["url"].ToString());
                    ticketQueue.Enqueue(item);
                }


                btnBuy.Text = "下一单";
                var ticketItem = ticketQueue.Dequeue();
                BuyTicket(ticketItem);
            }
            else
            {
                var ticketItem = ticketQueue.Dequeue();
                BuyTicket(ticketItem);
            }
        }

        private void BuyTicket(TicketsQueueItem ticketItem)
        {
            browser_type = BrowserType.VisitBuy;
            MarkTicketAsBought(ticketItem.Id);
            browser.Navigate(ticketItem.TicketPageUrl);
        }

        private void MarkTicketAsBought(string ticketId)
        {
            DAHelper.UpdateTicketStatus(ticketId, TicketStatus.HasBeenBought);
            foreach (DataRow row in dtTickets.Rows)
            {
                if (row["id"].ToString().Trim() == ticketId)
                {
                    row["status"] = TicketStatus.HasBeenBought;
                }
            }
        }

        private void btnFilter_Click(object sender, EventArgs e)
        {
            this.tabControl1.SelectedIndex = 1;
            if (dtTickets == null) return;
            dvTickets = new DataView(dtTickets);

            dvTickets.RowFilter = string.Format("status<>'{0}'", TicketStatus.HasBeenBought);
            dvTickets = new DataView(dvTickets.ToTable());
            
            if (!string.IsNullOrWhiteSpace(this.txtPriceStart.Text))
            {
                dvTickets.RowFilter = string.Format("price >= '{0}'", txtPriceStart.Text.Trim());
                dvTickets = new DataView(dvTickets.ToTable());
            }
            if (!string.IsNullOrWhiteSpace(this.txtPriceEnd.Text))
            {
                dvTickets.RowFilter = string.Format("price <= '{0}'", txtPriceEnd.Text.Trim());
                dvTickets = new DataView(dvTickets.ToTable());
            }
            if (!string.IsNullOrWhiteSpace(this.txtProgressStart.Text))
            {
                dvTickets.RowFilter = string.Format("progress >= '{0}'", decimal.Parse(this.txtProgressStart.Text.Trim()) / 100);
            }
            if (!string.IsNullOrWhiteSpace(this.txtProgressEnd.Text))
            {
                dvTickets.RowFilter = string.Format("progress <= '{0}'", decimal.Parse(this.txtProgressEnd.Text.Trim()) / 100);
            }


            //refresh UI
            gvTickets.DataSource = dvTickets;
            gvTickets.Refresh();

            ShowTotalRecords(dvTickets.Count);
        }

        private void menuItemExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void menuItemUpdateDatabase_Click(object sender, EventArgs e)
        {
            toolStripStatusMessage.Text = string.Empty;
            btnBuy.Text = "购买";

            /*****Get ticket from Taobao*******/
            tabControl1.SelectedIndex = 1;
            ShowUserMessage("正在更新数据库.....请稍等");
            DAHelper.ClearTickets();
            totalTicketsNumber = 0;

            //Get tickets using non-UI thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(DoRetriveTickets);
            worker.RunWorkerAsync();
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
        }

        private void menuItemClear_Click(object sender, EventArgs e)
        {
            var dialogResult =  MessageBox.Show(this, "清空数据库？", "确实", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);

            if (dialogResult == System.Windows.Forms.DialogResult.OK)
            {
                DAHelper.ClearTickets();
            }
        }

        private void toolStripMenuItemShowBoughtRecords_Click(object sender, EventArgs e)
        {

        }
    }
}
