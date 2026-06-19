#!/bin/bash

# Fix hardcoded Models.* references
find WebHomestay -name "*.cs" -type f -exec sed -i \
  's/Models\.AdminChatSession/WebHomestay.Models.Entities.Chat.AdminChatSession/g; s/Models\.Room\b/WebHomestay.Models.Entities.Core.Room/g' {} \;

# Fix namespace conflicts in specific files
files_with_conflicts=(
  "WebHomestay/Services/Chat/AdminChatQuickSendService.cs"
  "WebHomestay/Services/PublicBookingRoomExplanationService.cs"
  "WebHomestay/Services/Infrastructure/BulkImportService.cs"
  "WebHomestay/Services/ContextAwareBookingConductor.cs"
)

for file in "${files_with_conflicts[@]}"; do
  if [ -f "$file" ]; then
    # Replace WebHomestay.Models.Room with fully qualified
    sed -i 's/WebHomestay\.Models\.Room/WebHomestay.Models.Entities.Core.Room/g' "$file"
  fi
done

echo "Fixed hardcoded references"
