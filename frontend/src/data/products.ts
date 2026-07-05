export interface Product {
  slug: string
  name: string
  description: string
  bestFor: string[]
}

export const products: Product[] = [
  {
    slug: "hardwood-mulch-chips",
    name: "Hardwood Mulch Chips",
    description:
      "Coarse, long-lasting chips milled from oak, maple, and ash. Slow to break down, so they hold their look and coverage all season.",
    bestFor: [
      "Garden bed mulching",
      "Weed suppression",
      "Retaining soil moisture",
    ],
  },
  {
    slug: "softwood-landscape-chips",
    name: "Softwood Landscape Chips",
    description:
      "Lighter pine and fir chips with a fresh, natural scent. Break down faster than hardwood, enriching the soil as they decompose.",
    bestFor: ["Flower beds", "Pathways", "Playgrounds and soft ground cover"],
  },
  {
    slug: "colored-decorative-chips",
    name: "Colored Decorative Chips",
    description:
      "Dyed in rich browns, reds, and blacks for a polished, uniform look that holds color far longer than natural chips.",
    bestFor: [
      "Front yard curb appeal",
      "Commercial landscaping",
      "Around trees and borders",
    ],
  },
  {
    slug: "fine-garden-chips",
    name: "Fine Garden Chips",
    description:
      "Finely shredded chips that pack tightly and break down quickly, making them ideal for building healthy topsoil over time.",
    bestFor: ["Vegetable gardens", "Raised beds", "Composting"],
  },
  {
    slug: "biomass-fuel-chips",
    name: "Biomass Fuel Chips",
    description:
      "Dense, dry chips processed for consistent burn quality, sized for efficient loading and combustion.",
    bestFor: ["Wood-fired boilers", "Biomass heating systems", "Kilns"],
  },
  {
    slug: "playground-safety-chips",
    name: "Playground Safety Chips",
    description:
      "Engineered wood fiber chips, screened smooth and tested for fall-height safety under play equipment.",
    bestFor: [
      "Playgrounds",
      "Public parks",
      "Impact-absorbing ground cover",
    ],
  },
]
