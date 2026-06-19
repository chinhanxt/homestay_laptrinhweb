#!/bin/bash

# Fix all Views @model declarations
find WebHomestay/Views -name "*.cshtml" -exec sed -i \
  -e 's/@model IEnumerable<WebHomestay\.Models\.Room>/@model IEnumerable<WebHomestay.Models.Entities.Core.Room>/g' \
  -e 's/@model IEnumerable<WebHomestay\.Models\.AdminUser>/@model IEnumerable<WebHomestay.Models.Entities.Core.AdminUser>/g' \
  -e 's/@model WebHomestay\.Models\.AdminUser/@model WebHomestay.Models.Entities.Core.AdminUser/g' \
  -e 's/@model WebHomestay\.Models\.RoomSlotTemplate/@model WebHomestay.Models.Entities.Slots.RoomSlotTemplate/g' \
  {} \;

# Fix ChatHub - replace WebHomestay.Models.AdminChatSession
find WebHomestay/Hubs -name "*.cs" -exec sed -i \
  's/WebHomestay\.Models\.AdminChatSession/WebHomestay.Models.Entities.Chat.AdminChatSession/g' \
  {} \;

# Fix AdminChatMonitorController
sed -i 's/WebHomestay\.Models\.AdminChatSession/WebHomestay.Models.Entities.Chat.AdminChatSession/g' \
  WebHomestay/Controllers/Admin/AdminChatMonitorController.cs

# Fix AdminChatQuickSendService
sed -i 's/WebHomestay\.Models\.Room/WebHomestay.Models.Entities.Core.Room/g' \
  WebHomestay/Services/Chat/AdminChatQuickSendService.cs

echo "Comprehensive fixes applied"
