using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BuyLottery.Common
{
    class TicketsQueueItem
    {
        public string Id { get; set; }
        public string TicketPageUrl { get; set; }

        public TicketsQueueItem(string id, string ticketPageUrl)
        {
            this.Id = id;
            this.TicketPageUrl = ticketPageUrl;
        }
    }
}
