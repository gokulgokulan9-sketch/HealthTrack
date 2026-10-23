using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    public partial class ChatMessage
    {
        [Key]
        public int MessageId { get; set; }

        public int SenderId { get; set; }

        public int ReceiverId { get; set; }

        [Required]
        public string MessageText { get; set; } = null!;

        [Column(TypeName = "datetime")]
        public DateTime SentAt { get; set; }

        public bool IsRead { get; set; }

        // Sender
        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; } = null!;

        // Receiver
        [ForeignKey("ReceiverId")]
        public virtual User Receiver { get; set; } = null!;
    }
}