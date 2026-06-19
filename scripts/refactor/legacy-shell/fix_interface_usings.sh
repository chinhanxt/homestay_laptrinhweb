#!/bin/bash

# Add using statements to all interface files
for file in WebHomestay/Services/I*.cs; do
    if [ -f "$file" ]; then
        # Check if file doesn't have entity usings yet
        if ! grep -q "using WebHomestay.Models.Entities.Core;" "$file" 2>/dev/null; then
            # Add after namespace declaration or at the top
            sed -i '1i using WebHomestay.Models.Entities.Core;\nusing WebHomestay.Models.Entities.Slots;\nusing WebHomestay.Models.Entities.Chat;\nusing WebHomestay.Models.Entities.AI;\nusing WebHomestay.Models.Enums;\nusing WebHomestay.Models.DTOs.Booking;\nusing WebHomestay.Models.DTOs.AI;\nusing WebHomestay.Models.Configuration;\nusing WebHomestay.Models.ViewModels;' "$file"
        fi
    fi
done

echo "Added using statements to interface files"
