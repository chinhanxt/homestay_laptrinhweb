#!/bin/bash

# Fix Models.AI references
find WebHomestay -name "*.cs" -type f -exec sed -i 's/using WebHomestay\.Models\.AI;/using WebHomestay.Models.Configuration;\nusing WebHomestay.Models.DTOs.AI;\nusing WebHomestay.Models.Entities.AI;/g' {} \;

# Add missing entity usings to files that need them
for file in WebHomestay/Controllers/**/*.cs WebHomestay/Services/**/*.cs WebHomestay/Hubs/*.cs WebHomestay/Models/**/*.cs; do
    if [ -f "$file" ]; then
        # Check if file uses Models but doesn't have new namespaces
        if grep -q "using WebHomestay.Models;" "$file" 2>/dev/null; then
            # Add all entity namespaces after the Models using
            sed -i '/using WebHomestay\.Models;/a using WebHomestay.Models.Entities.Core;\nusing WebHomestay.Models.Entities.Slots;\nusing WebHomestay.Models.Entities.Chat;\nusing WebHomestay.Models.Entities.AI;\nusing WebHomestay.Models.Enums;\nusing WebHomestay.Models.DTOs.Booking;\nusing WebHomestay.Models.DTOs.AI;\nusing WebHomestay.Models.Configuration;\nusing WebHomestay.Models.ViewModels;' "$file"
            # Remove the old generic using
            sed -i '/^using WebHomestay\.Models;$/d' "$file"
        fi
    fi
done

echo "Fixed using statements"
