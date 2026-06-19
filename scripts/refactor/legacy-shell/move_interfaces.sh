#!/bin/bash

# Move interfaces to appropriate folders
mv WebHomestay/Services/IAvailabilityService.cs WebHomestay/Services/Booking/
mv WebHomestay/Services/IBookingCreationService.cs WebHomestay/Services/Booking/
mv WebHomestay/Services/IBookingCancellationService.cs WebHomestay/Services/Booking/

mv WebHomestay/Services/ISlotManagementService.cs WebHomestay/Services/Slots/

mv WebHomestay/Services/IRoomBookingViewService.cs WebHomestay/Services/Room/
mv WebHomestay/Services/IPublicBookingRoomExplanationService.cs WebHomestay/Services/Room/

mv WebHomestay/Services/IBranchLeadTimeService.cs WebHomestay/Services/Branch/

mv WebHomestay/Services/IAdminChatService.cs WebHomestay/Services/Chat/
mv WebHomestay/Services/IAdminChatQuickSendService.cs WebHomestay/Services/Chat/

mv WebHomestay/Services/ISettingService.cs WebHomestay/Services/Settings/

mv WebHomestay/Services/IMailService.cs WebHomestay/Services/Infrastructure/
mv WebHomestay/Services/IImageMaskingService.cs WebHomestay/Services/Infrastructure/
mv WebHomestay/Services/IStatisticsService.cs WebHomestay/Services/Infrastructure/

# Update namespaces
for file in WebHomestay/Services/Booking/I*.cs; do
    [ -f "$file" ] && sed -i 's/namespace WebHomestay.Services/namespace WebHomestay.Services.Booking/' "$file"
done

for file in WebHomestay/Services/Slots/I*.cs; do
    [ -f "$file" ] && sed -i 's/namespace WebHomestay.Services/namespace WebHomestay.Services.Slots/' "$file"
done

for file in WebHomestay/Services/Room/I*.cs; do
    [ -f "$file" ] && sed -i 's/namespace WebHomestay.Services/namespace WebHomestay.Services.Room/' "$file"
done

for file in WebHomestay/Services/Branch/I*.cs; do
    [ -f "$file" ] && sed -i 's/namespace WebHomestay.Services/namespace WebHomestay.Services.Branch/' "$file"
done

for file in WebHomestay/Services/Chat/I*.cs; do
    [ -f "$file" ] && sed -i 's/namespace WebHomestay.Services/namespace WebHomestay.Services.Chat/' "$file"
done

for file in WebHomestay/Services/Settings/I*.cs; do
    [ -f "$file" ] && sed -i 's/namespace WebHomestay.Services/namespace WebHomestay.Services.Settings/' "$file"
done

for file in WebHomestay/Services/Infrastructure/I*.cs; do
    [ -f "$file" ] && sed -i 's/namespace WebHomestay.Services/namespace WebHomestay.Services.Infrastructure/' "$file"
done

echo "Moved and updated interfaces"
