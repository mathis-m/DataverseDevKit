import React, { useEffect, useRef, useState } from 'react';
import * as d3 from 'd3';
import { ComponentResult } from '../types';
import { makeStyles, tokens, Button } from '@fluentui/react-components';
import { ZoomIn20Regular, ZoomOut20Regular, ArrowReset20Regular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  container: {
    position: 'relative',
    width: '100%',
    height: '100%',
  },
  controls: {
    position: 'absolute',
    top: tokens.spacingVerticalM,
    right: tokens.spacingHorizontalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    zIndex: 10,
  },
  svg: {
    width: '100%',
    height: '100%',
    cursor: 'grab',
    ':active': {
      cursor: 'grabbing',
    },
  },
});

interface LayerCirclePackingProps {
  components: ComponentResult[];
  width?: number;
  height?: number;
}

export const LayerCirclePacking: React.FC<LayerCirclePackingProps> = ({
  components,
  width = 1200,
  height = 800,
}) => {
  const styles = useStyles();
  const svgRef = useRef<SVGSVGElement>(null);
  const [zoom, setZoom] = useState<d3.ZoomBehavior<SVGSVGElement, unknown> | null>(null);

  useEffect(() => {
    if (!svgRef.current || components.length === 0) return;

    // Clear previous content
    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    // Prepare hierarchical data: Solution -> ComponentType -> Components
    const solutionMap = new Map<string, Map<string, ComponentResult[]>>();

    components.forEach(component => {
      component.layerSequence.forEach(solution => {
        if (!solutionMap.has(solution)) {
          solutionMap.set(solution, new Map());
        }
        const typeMap = solutionMap.get(solution)!;
        if (!typeMap.has(component.componentType)) {
          typeMap.set(component.componentType, []);
        }
        typeMap.get(component.componentType)!.push(component);
      });
    });

    // Create hierarchy
    const hierarchyData: any = {
      name: 'Root',
      children: Array.from(solutionMap.entries()).map(([solution, typeMap]) => ({
        name: solution,
        children: Array.from(typeMap.entries()).map(([type, comps]) => ({
          name: type,
          children: comps.map(c => ({
            name: c.displayName || c.logicalName,
            value: c.layerSequence.length, // Size by layer count
          })),
        })),
      })),
    };

    const root = d3.hierarchy(hierarchyData)
      .sum((d: any) => d.value || 0)
      .sort((a, b) => (b.value || 0) - (a.value || 0));

    const pack = d3.pack()
      .size([width - 20, height - 20])
      .padding(3);

    pack(root as any);

    // Create zoom behavior
    const zoomBehavior = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.5, 10])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
      });

    svg.call(zoomBehavior);
    setZoom(zoomBehavior as any);

    const g = svg.append('g')
      .attr('transform', `translate(${10},${10})`);

    const color = d3.scaleOrdinal(d3.schemeTableau10);

    // Draw circles
    const node = g.selectAll('g')
      .data(root.descendants())
      .join('g')
      .attr('transform', (d: any) => `translate(${d.x},${d.y})`)
      .style('cursor', 'pointer');

    node.append('circle')
      .attr('r', (d: any) => d.r)
      .attr('fill', (d: any) => {
        if (d.depth === 0) return 'none';
        if (d.depth === 1) return color(d.data.name); // Solutions
        if (d.depth === 2) return d3.color(color(d.parent.data.name))!.brighter(0.5).toString(); // Types
        return d3.color(color(d.parent.parent.data.name))!.brighter(1).toString(); // Components
      })
      .attr('stroke', (d: any) => d.depth > 0 ? '#fff' : 'none')
      .attr('stroke-width', 2)
      .attr('opacity', 0.8)
      .on('mouseover', function() {
        d3.select(this).attr('opacity', 1);
      })
      .on('mouseout', function() {
        d3.select(this).attr('opacity', 0.8);
      });

    // Add text for larger circles
    node.filter((d: any) => d.r > 20)
      .append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', '0.3em')
      .style('font-size', (d: any) => Math.min(d.r / 3, 14) + 'px')
      .style('fill', (d: any) => d.depth <= 2 ? '#000' : '#666')
      .style('pointer-events', 'none')
      .style('font-weight', (d: any) => d.depth <= 2 ? 'bold' : 'normal')
      .text((d: any) => {
        const name = d.data.name;
        if (name.length > 15) {
          return name.substring(0, 12) + '...';
        }
        return name;
      });

    node.append('title')
      .text((d: any) => {
        if (d.depth === 1) return `Solution: ${d.data.name} (${d.children?.length || 0} types)`;
        if (d.depth === 2) return `Type: ${d.data.name} (${d.children?.length || 0} components)`;
        if (d.depth === 3) return `Component: ${d.data.name} (${d.value} layers)`;
        return d.data.name;
      });

  }, [components, width, height]);

  const handleZoomIn = () => {
    if (zoom && svgRef.current) {
      d3.select(svgRef.current).transition().call(zoom.scaleBy as any, 1.3);
    }
  };

  const handleZoomOut = () => {
    if (zoom && svgRef.current) {
      d3.select(svgRef.current).transition().call(zoom.scaleBy as any, 0.7);
    }
  };

  const handleReset = () => {
    if (zoom && svgRef.current) {
      d3.select(svgRef.current).transition().call(zoom.transform as any, d3.zoomIdentity);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.controls}>
        <Button icon={<ZoomIn20Regular />} onClick={handleZoomIn} title="Zoom In" />
        <Button icon={<ZoomOut20Regular />} onClick={handleZoomOut} title="Zoom Out" />
        <Button icon={<ArrowReset20Regular />} onClick={handleReset} title="Reset View" />
      </div>
      <svg ref={svgRef} className={styles.svg} width={width} height={height} />
    </div>
  );
};
