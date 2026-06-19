#!/bin/bash

# Add service namespace usings to files that reference services
for file in WebHomestay/Controllers/**/*.cs WebHomestay/Services/**/*.cs; do
    if [ -f "$file" ]; then
        # Check if file references service interfaces but doesn't have new namespaces
        if grep -qE "IAvailabilityService|IBookingCreationService|ISlotGenerationService|IBulkImportService|IPaymentQrSettingsService|IMailService" "$file" 2>/dev/null; then
            if ! grep -q "using WebHomestay.Services.Booking;" "$file" 2>/dev/null; then
                # Add service namespaces at the beginning
                sed -i '1i using WebHomestay.Services.Booking;\nusing WebHomestay.Services.Slots;\nusing WebHomestay.Services.Room;\nusing WebHomestay.Services.Branch;\nusing WebHomestay.Services.Chat;\nusing WebHomestay.Services.Settings;\nusing WebHomestay.Services.Infrastructure;' "$file"
            fi
        fi
    fi
done

echo "Fixed service using statements"
